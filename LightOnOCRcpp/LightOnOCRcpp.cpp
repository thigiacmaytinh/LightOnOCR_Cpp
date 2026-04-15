#include "LightOnOCRcpp.h"

#include <stdexcept>
#include <iostream>

#include "mtmd-helper.h"

LightOnOCR::LightOnOCR(const std::string& model_path, const std::string& mmproj_path, int n_ctx) {
    llama_backend_init();

    llama_model_params model_params = llama_model_default_params();
    model_params.n_gpu_layers = 9999;

    model = llama_model_load_from_file(model_path.c_str(), model_params);
    if (!model) throw std::runtime_error("Failed to load main model: " + model_path);

    mtmd_context_params mtmd_params = mtmd_context_params_default();
    mtmd_params.use_gpu = true;
    mtmd_params.n_threads = n_threads;

    ctx_mtmd = mtmd_init_from_file(mmproj_path.c_str(), model, mtmd_params);
    if (!ctx_mtmd) throw std::runtime_error("Failed to load mmproj adapter via MTMD");

    llama_context_params ctx_params = llama_context_default_params();
    ctx_params.n_ctx = n_ctx;
    ctx_params.n_threads = n_threads;
    ctx_params.n_threads_batch = n_threads;

    ctx_params.no_perf = true;

    ctx = llama_init_from_model(model, ctx_params);
    if (!ctx) throw std::runtime_error("Failed to create llama context");

    sampler = llama_sampler_chain_init(llama_sampler_chain_default_params());
    llama_sampler_chain_add(sampler, llama_sampler_init_greedy());

    batch = llama_batch_init(n_batch, 0, 1);

    max_ctx = n_ctx;
}

LightOnOCR::~LightOnOCR() {
    if (sampler) llama_sampler_free(sampler);
    if (batch.token) llama_batch_free(batch);
    if (ctx_mtmd) mtmd_free(ctx_mtmd);
    if (ctx) llama_free(ctx);
    if (model) llama_model_free(model);
    llama_backend_free();
}

std::string LightOnOCR::process_image_stream(const std::string& image_path,
    const std::string& prompt,
    std::function<void(const std::string&)> on_token_generated) {

    llama_memory_clear(llama_get_memory(ctx), false);
    llama_pos n_past = 0;
    std::string full_result;

    const llama_vocab* vocab = llama_model_get_vocab(model);

    // 1. Load image bitmap
    mtmd_bitmap* bmp = mtmd_helper_bitmap_init_from_file(ctx_mtmd, image_path.c_str());
    if (!bmp) throw std::runtime_error("Failed to read image file");

    // 2. Build prompt with the media marker
    //std::string full_prompt = std::string(mtmd_default_marker()) + "\n" + prompt;
    std::string full_prompt = std::string(mtmd_default_marker()) + "\n<|im_start|>assistant\n";
    mtmd_input_text input_text;
    input_text.text = full_prompt.c_str();
    input_text.add_special = true;
    input_text.parse_special = true;

    // 3. Tokenize prompt + image into chunks
    mtmd_input_chunks* chunks = mtmd_input_chunks_init();
    const mtmd_bitmap* bitmaps[] = { bmp };
    int32_t tok_res = mtmd_tokenize(ctx_mtmd, chunks, &input_text, bitmaps, 1);
    mtmd_bitmap_free(bmp);
    if (tok_res != 0) {
        mtmd_input_chunks_free(chunks);
        throw std::runtime_error("Failed to tokenize input");
    }

    // 4. Evaluate all chunks into the llama context
    int32_t eval_res = mtmd_helper_eval_chunks(ctx_mtmd, ctx, chunks, n_past, 0, n_batch, true, &n_past);
    mtmd_input_chunks_free(chunks);
    if (eval_res != 0) throw std::runtime_error("Failed to evaluate chunks");

    // 5. Generation loop
    while (true) {
        llama_token id = llama_sampler_sample(sampler, ctx, -1);
        llama_sampler_accept(sampler, id);

        if (llama_vocab_is_eog(vocab, id)) break;

        char buf[128];
        int n = llama_token_to_piece(vocab, id, buf, sizeof(buf), 0, true);
        if (n > 0) {
            std::string token_str(buf, n);
            full_result += token_str;
            if (on_token_generated) on_token_generated(token_str);
        }

        batch.n_tokens = 0;
        batch.token[0] = id;
        batch.pos[0] = n_past++;
        batch.seq_id[0][0] = 0;
        batch.n_seq_id[0] = 1;
        batch.logits[0] = 1;
        batch.n_tokens = 1;

        if (llama_decode(ctx, batch) != 0) break;
    }

    return full_result;
}

std::string LightOnOCR::process_image_buffer_stream(
    const unsigned char* image_buffer,
    size_t buffer_size,
    std::function<void(const std::string&)> on_token_generated)
{
    llama_memory_clear(llama_get_memory(ctx), false);
    llama_pos n_past = 0;
    std::string full_result;
    const llama_vocab* vocab = llama_model_get_vocab(model);

    // 1. Load image from RAM buffer
    mtmd_bitmap* bmp = mtmd_helper_bitmap_init_from_buf(ctx_mtmd, image_buffer, buffer_size);
    if (!bmp) throw std::runtime_error("Failed to read image buffer");

    // 2. Build exact ChatML prompt to prevent double tags
    std::string full_prompt = std::string(mtmd_default_marker()) + "\n<|im_start|>assistant\n";

    mtmd_input_text input_text;
    input_text.text = full_prompt.c_str();
    input_text.add_special = true;
    input_text.parse_special = true;

    // 3. Tokenize & Evaluate
    mtmd_input_chunks* chunks = mtmd_input_chunks_init();
    const mtmd_bitmap* bitmaps[] = { bmp };
    int32_t tok_res = mtmd_tokenize(ctx_mtmd, chunks, &input_text, bitmaps, 1);
    mtmd_bitmap_free(bmp);
    if (tok_res != 0) {
        mtmd_input_chunks_free(chunks);
        throw std::runtime_error("Failed to tokenize input");
    }

    int32_t eval_res = mtmd_helper_eval_chunks(ctx_mtmd, ctx, chunks, n_past, 0, n_batch, true, &n_past);
    mtmd_input_chunks_free(chunks);
    if (eval_res != 0) throw std::runtime_error("Failed to evaluate chunks");

    // 4. Generation Loop
    while (true) {
        llama_token id = llama_sampler_sample(sampler, ctx, -1);
        llama_sampler_accept(sampler, id);

        if (llama_vocab_is_eog(vocab, id)) break;

        char buf[128];
        int n = llama_token_to_piece(vocab, id, buf, sizeof(buf), 0, true);
        if (n > 0) {
            std::string token_str(buf, n);
            full_result += token_str;
            if (on_token_generated) on_token_generated(token_str); // Trigger C# callback
        }

        batch.n_tokens = 0;
        batch.token[0] = id;
        batch.pos[0] = n_past++; // Increment position!
        batch.seq_id[0][0] = 0;
        batch.n_seq_id[0] = 1;
        batch.logits[0] = 1;
        batch.n_tokens = 1;

        if (llama_decode(ctx, batch) != 0) break;
    }
    return full_result;
}
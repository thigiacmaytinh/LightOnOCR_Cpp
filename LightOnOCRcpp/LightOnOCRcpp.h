#pragma once

#include <string>
#include <vector>
#include <functional>

#include "llama.h"
#include "mtmd.h"

class LightOnOCR {
private:
    llama_model* model = nullptr;
    llama_context* ctx = nullptr;
    mtmd_context* ctx_mtmd = nullptr;
    llama_sampler* sampler = nullptr;
    llama_batch batch{};

    int n_threads = 8;
    int n_batch = 512;
    int max_ctx = 8192;

public:
    // Constructor initializes models and context
    LightOnOCR(const std::string& model_path, const std::string& mmproj_path, int n_ctx = 8192);

    // Destructor frees resources safely
    ~LightOnOCR();

    // Prevent copying (to avoid double-freeing pointers)
    LightOnOCR(const LightOnOCR&) = delete;
    LightOnOCR& operator=(const LightOnOCR&) = delete;

    // Main inference method with streaming callback
    std::string process_image_stream(const std::string& image_path,
        const std::string& prompt,
        std::function<void(const std::string&)> on_token_generated);

	// Method to process image from a memory buffer
    std::string process_image_buffer_stream(
        const unsigned char* image_buffer,
        size_t buffer_size,
        std::function<void(const std::string&)> on_token_generated);
};

// Separate Utility Class for File Operations
class Utils {
public:
    // Converts Markdown text to a DOCX file using system Pandoc
    static bool save_markdown_to_docx(const std::string& markdown_content, const std::string& output_filename);

    // Fallback method to save raw text or markdown
    static bool save_to_text(const std::string& text, const std::string& output_filename);
};

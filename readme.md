# LightOnOCR_Cpp

AI-powered OCR application that extracts text from images and PDFs using a multimodal vision-language model (1B parameters). Outputs structured markdown with bounding box detection, exportable to Word (.docx), markdown, or plain text.

## Architecture

The project has **3 components** across 2 Visual Studio solutions:

| Project | Language | Type | Purpose |
|---------|----------|------|---------|
| **LightOnOCRcpp** | C++ (native) | Console EXE | Core inference engine wrapping llama.cpp |
| **LightOnOCR** | C++/CLI | DLL | Managed wrapper bridging C++ ↔ .NET |
| **LightOnOCR_UI** | C# WPF | Desktop App | GUI with batch processing, drag-drop, live streaming |

```
LightOnOCR_UI.sln   ← Full app (all 3 projects)
LightOnOCRcpp.sln   ← CLI-only
```

## Prerequisites

- **Visual Studio 2022** (v17.14+) with C++ Desktop and .NET Desktop workloads
- **.NET 8.0 SDK**
- **C++20** toolset (v143)
- **Windows 10+**, x64 only
- **Pandoc** (for markdown → .docx conversion)

## Models

[Download model](https://huggingface.co/noctrex/LightOnOCR-2-1B-bbox-GGUF) and place in `bin/model/`:

| Model | Purpose |
|-------|---------|
| `LightOnOCR-2-1B-bbox-BF16.gguf` | Main LLM + vision backbone (BF16) |
| `mmproj-F32.gguf` | Multimodal projection adapter |

## Build from Source

### Step 1: Clone and prepare dependencies

```
copy lib/llama.cpp/lib/*.dll -> bin/
copy pandoc.exe -> bin/
```

Ensure model files are in `bin/model/`.

### Step 2: Build the full application (GUI + CLI)

1. Open `LightOnOCR_UI.sln` in Visual Studio 2022
2. Select **Release | x64**
3. **Build** → **Build Solution** (builds all 3 projects in dependency order)

### Step 3: Build CLI only (optional)

1. Open `LightOnOCRcpp.sln` in Visual Studio 2022
2. Select **Release-static | x64**
3. Build → generates `LightOnOCRcpp.exe`

### Output structure

```
bin/
├── LightOnOCR_UI.exe              ← WPF application
├── LightOnOCR.dll                 ← C++/CLI wrapper
├── LightOnOCRcpp.exe              ← CLI tool
├── pandoc.exe                     ← Markdown → DOCX converter
├── *.dll                          ← llama.cpp runtime DLLs
├── model/
│   ├── LightOnOCR-2-1B-bbox-BF16.gguf
│   └── mmproj-F32.gguf
└── runtimes/                      ← PDFium native libs
```

## Usage

### GUI Application

```bash
bin\LightOnOCR_UI.exe
```

1. Wait for the model to load (status bar shows progress)
2. Click **"+ Select Images / PDFs"** or drag files into the queue
3. Reorder items by dragging if needed
4. Click **"START PROCESSING"** — tokens stream in real-time
5. Export results:
   - Single file → **Save File (.docx)**
   - Multiple files → **Save as ZIP (.zip)** or **Combine Files**

**Supported input:** PNG, JPG, JPEG, PDF (multi-page)
**Export formats:** .docx, .md, .txt, .zip

**UI features:**
- Real-time token streaming during OCR
- Automatic bounding box detection and image cropping
- PDF multi-page processing at 300 DPI
- Dark / Light theme toggle
- Batch export to ZIP archive

### CLI Application

```bash
LightOnOCRcpp.exe <model_path> <mmproj_path> <image_path> [prompt]
```

**Example:**

```bash
LightOnOCRcpp.exe model\LightOnOCR-2-1B-bbox-BF16.gguf model\mmproj-F32.gguf document.png
```

Streams extracted text with bounding box coordinates to stdout.

## Dependencies

| Package | Purpose |
|---------|---------|
| [llama.cpp](https://github.com/ggml-org/llama.cpp) | LLM inference engine (GGML/GGUF) |
| [PDFtoImage](https://www.nuget.org/packages/PDFtoImage) | PDF → image conversion |
| [SkiaSharp](https://www.nuget.org/packages/SkiaSharp) | Image cropping and manipulation |
| [PDFium](https://www.nuget.org/packages/bblanchon.PDFium.Win32) | Native PDF rendering |
| [Pandoc](https://pandoc.org/) | Markdown → DOCX conversion |
#include "OcrWrapper.h"
#include "LightOnOCRcpp.h"
#include <msclr/marshal_cppstd.h>

using namespace System;

namespace LightOnOCRWrapper 
{
    // Helper function to safely convert C# string to C++ UTF-8 string
    std::string ToStdStringUTF8(String^ str) {
        if (String::IsNullOrEmpty(str)) return "";
        array<Byte>^ utf8Bytes = System::Text::Encoding::UTF8->GetBytes(str);
        cli::pin_ptr<Byte> pinnedBytes = &utf8Bytes[0];
        return std::string(reinterpret_cast<char*>(pinnedBytes), utf8Bytes->Length);
    }

    // --- NativeCallback Implementation ---
    NativeCallback::NativeCallback(Action<array<Byte>^>^ cb) : callback(cb) {
    }

    void NativeCallback::operator()(const std::string& token) const {
        if (static_cast<System::Action<array<Byte>^>^>(callback) != nullptr) {
            if (token.empty()) return;

            // Pass raw bytes to C# to prevent UTF-8 splitting corruption
            array<Byte>^ bytes = gcnew array<Byte>((int)token.length());
            System::Runtime::InteropServices::Marshal::Copy((IntPtr)(void*)token.data(), bytes, 0, (int)token.length());
            static_cast<System::Action<array<Byte>^>^>(callback)->Invoke(bytes);
        }
    }

    // --- OcrEngine Implementation ---
    OcrEngine::OcrEngine(String^ modelPath, String^ mmprojPath) {
        std::string stdModel = msclr::interop::marshal_as<std::string>(modelPath);
        std::string stdProj = msclr::interop::marshal_as<std::string>(mmprojPath);
        nativeOcr = new LightOnOCR(stdModel, stdProj, 8192); // Pass default context size or add parameter
    }

    OcrEngine::~OcrEngine() {
        this->!OcrEngine();
    }

    OcrEngine::!OcrEngine() {
        if (nativeOcr) {
            delete nativeOcr;
            nativeOcr = nullptr;
        }
    }

    String^ OcrEngine::ProcessImageBytes(array<Byte>^ imageBytes, Action<array<Byte>^>^ onTokenGenerated) {
        cli::pin_ptr<Byte> pinnedBytes = &imageBytes[0];
        const unsigned char* nativeBuffer = pinnedBytes;
        size_t bufferSize = imageBytes->Length;

        NativeCallback cb(onTokenGenerated);
        std::function<void(const std::string&)> func = cb;

        std::string result = nativeOcr->process_image_buffer_stream(
            nativeBuffer,
            bufferSize,
            func
        );

        // Convert the final return string to UTF-8 safely
        array<Byte>^ resBytes = gcnew array<Byte>((int)result.length());
        if (result.length() > 0) {
            System::Runtime::InteropServices::Marshal::Copy((IntPtr)(void*)result.data(), resBytes, 0, (int)result.length());
        }
        return System::Text::Encoding::UTF8->GetString(resBytes);
    }

}

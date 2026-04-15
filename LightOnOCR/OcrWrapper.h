#pragma once

// Forward declare the native class so we don't have to expose its internals everywhere
class LightOnOCR;

#include <string>
#include <functional>
#include <msclr/auto_gcroot.h>


namespace LightOnOCRWrapper {

    // The unmanaged struct holding the managed delegate
    struct NativeCallback {
        gcroot<System::Action<array<System::Byte>^>^> callback;
        NativeCallback(System::Action<array<System::Byte>^>^ cb);
        void operator()(const std::string& token) const;
    };

    // The Managed Wrapper Class
    public ref class OcrEngine {
    private:
        LightOnOCR* nativeOcr;

    public:
        OcrEngine(System::String^ modelPath, System::String^ mmprojPath);

        // Destructors
        ~OcrEngine();
        !OcrEngine();

        // Main Method
        System::String^ ProcessImageBytes(array<System::Byte>^ imageBytes, System::Action<array<System::Byte>^>^ onTokenGenerated);
    };
}

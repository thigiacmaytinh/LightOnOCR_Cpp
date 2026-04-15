#include "LightOnOCRcpp.h"
#include <iostream>
#include <stdexcept>

int main(int argc, char* argv[]) {
    if (argc < 4) {
        std::cerr << "Usage: LightOnOCRcpp <model_path> <mmproj_path> <image_path> [prompt]\n";
        return 1;
    }

    const std::string model_path  = argv[1];
    const std::string mmproj_path = argv[2];
    const std::string image_path  = argv[3];
    const std::string prompt      = (argc >= 5) ? argv[4] : "";

    try {
        LightOnOCR ocr(model_path, mmproj_path);

        std::string result = ocr.process_image_stream(image_path, prompt,
            [](const std::string& token) {
                std::cout << token << std::flush;
            });

        std::cout << "\n";
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }

    return 0;
}

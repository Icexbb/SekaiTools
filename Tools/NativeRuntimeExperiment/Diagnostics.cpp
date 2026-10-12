#include <opencv2/core.hpp>
#include <opencv2/core/utility.hpp>
#include <cstring>
#include <sstream>
#include <limits>

// Research-only, read-only export. Query the same linked core used by cvextern.
extern "C" __declspec(dllexport) int __cdecl sekaiExperimentInfo(char* output, int capacity)
{
    try {
        std::ostringstream stream;
        stream << "CpuFeatures=" << cv::getCPUFeaturesLine() << '\n'
               << "IppEnabled=" << cv::ipp::useIPP() << '\n'
               << "IppNotExact=" << cv::ipp::useIPP_NotExact() << '\n'
               << "IppVersion=" << cv::ipp::getIppVersion() << '\n'
               << "IppFeatures=" << std::hex << cv::ipp::getIppFeatures() << '\n';
        const std::string info = stream.str();
        if (info.size() >= static_cast<size_t>(std::numeric_limits<int>::max())) return -1;
        const int required = static_cast<int>(info.size()) + 1;
        if (output && capacity >= required) std::memcpy(output, info.c_str(), required);
        return required;
    } catch (...) { return -1; }
}

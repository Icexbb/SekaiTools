// Research only. Compile against the pinned OpenCV source and cvextern runtime.
// The caller must qualify and retain the plugin library until capture disposal.
#include <opencv2/imgproc.hpp>
#include <opencv2/core/utils/logger.hpp>
#include "cap_interface.hpp"
#include "plugin_capture_api.hpp"
#include <exception>
#include <cstring>
#include <stdexcept>

namespace {
class GrayPluginCapture final : public cv::IVideoCapture {
    const OpenCV_VideoIO_Capture_Plugin_API_v1_1* api_;
    CvPluginCapture handle_ = nullptr;
    struct Destination {
        cv::OutputArray output;
        unsigned calls = 0;
        std::exception_ptr error;
    };
    static CvResult CV_API_CALL receive(int stream, const unsigned char* data,
        int step, int width, int height, int type, void* context) noexcept {
        auto& destination = *static_cast<Destination*>(context);
        try {
            if (++destination.calls != 1 || stream != 0 || !data || width <= 0 ||
                height <= 0 || width > 16384 || height > 16384 ||
                type != CV_8UC3 || step < width * 3)
                throw std::runtime_error("Invalid synchronous capture frame");
            // Only the header is borrowed; cvtColor writes into owned output.
            cv::Mat borrowed(height, width, CV_8UC3, const_cast<unsigned char*>(data), step);
            cv::cvtColor(borrowed, destination.output, cv::COLOR_BGR2GRAY);
            return CV_ERROR_OK;
        } catch (...) {
            destination.error = std::current_exception();
            return CV_ERROR_FAIL;
        }
    }
public:
    GrayPluginCapture(const OpenCV_VideoIO_Capture_Plugin_API_v1_1* api,
        const char* video, int threads) : api_(api) {
        if (!api || !video || threads < 1 || threads > 64 ||
            api->api_header.valid_size < sizeof(*api) ||
            api->api_header.min_api_version > 1 || api->api_header.api_version < 1 ||
            api->v0.id != cv::CAP_FFMPEG || !api->v1.Capture_open_with_params ||
            !api->v0.Capture_release || !api->v0.Capture_getProperty ||
            !api->v0.Capture_setProperty || !api->v0.Capture_grab || !api->v0.Capture_retreive)
            throw std::runtime_error("Unsupported capture API");
        int parameters[] = { cv::CAP_PROP_N_THREADS, threads };
        if (api_->v1.Capture_open_with_params(video, 0, parameters, 1, &handle_) != CV_ERROR_OK || !handle_) {
            if (handle_) api_->v0.Capture_release(handle_);
            handle_ = nullptr;
            throw std::runtime_error("Cannot open gray capture");
        }
        if (getProperty(cv::CAP_PROP_N_THREADS) != threads ||
            getProperty(cv::CAP_PROP_HW_ACCELERATION) != cv::VIDEO_ACCELERATION_NONE) {
            api_->v0.Capture_release(handle_);
            handle_ = nullptr;
            throw std::runtime_error("Unexpected decoder configuration");
        }
    }
    ~GrayPluginCapture() override { if (handle_) api_->v0.Capture_release(handle_); }
    bool isOpened() const override { return handle_ != nullptr; }
    int getCaptureDomain() override { return cv::CAP_FFMPEG; }
    double getProperty(int property) const override {
        if (property == cv::CAP_PROP_FORMAT) return CV_8UC1;
        double value = 0;
        return api_->v0.Capture_getProperty(handle_, property, &value) == CV_ERROR_OK ? value : 0;
    }
    bool setProperty(int property, double value) override {
        // Format changes would invalidate the BGR callback contract.
        if (property == cv::CAP_PROP_FORMAT || property == cv::CAP_PROP_CONVERT_RGB) return false;
        return api_->v0.Capture_setProperty(handle_, property, value) == CV_ERROR_OK;
    }
    bool grabFrame() override { return api_->v0.Capture_grab(handle_) == CV_ERROR_OK; }
    bool retrieveFrame(int stream, cv::OutputArray output) override {
        Destination destination{output};
        auto result = api_->v0.Capture_retreive(handle_, stream, receive, &destination);
        if (destination.error) std::rethrow_exception(destination.error);
        if (result != CV_ERROR_OK) return false;
        if (destination.calls != 1) throw std::runtime_error("Missing capture callback");
        return true;
    }
};
class GrayVideoCapture final : public cv::VideoCapture {
public:
    GrayVideoCapture(const OpenCV_VideoIO_Capture_Plugin_API_v1_1* api,
        const char* video, int threads) { icap = cv::makePtr<GrayPluginCapture>(api, video, threads); }
};
}

extern "C" __declspec(dllexport) cv::VideoCapture* __cdecl sekaiCreateGrayCapture(
    const OpenCV_VideoIO_Capture_Plugin_API_v1_1* api, const char* video,
    int threads, char* error, int capacity) noexcept {
    try { return new GrayVideoCapture(api, video, threads); }
    catch (const std::exception& exception) {
        if (error && capacity > 0) {
            strncpy_s(error, capacity, exception.what(), _TRUNCATE);
        }
    } catch (...) {
        if (error && capacity > 0) strncpy_s(error, capacity, "Unknown native capture error", _TRUNCATE);
    }
    return nullptr;
}

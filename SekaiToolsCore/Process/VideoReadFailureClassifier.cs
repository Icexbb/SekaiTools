namespace SekaiToolsCore.Process;

internal enum VideoReadFailureAction
{
    Retry,
    EndOfStream,
    ReadFailed,
    CaptureError
}

internal static class VideoReadFailureClassifier
{
    public static VideoReadFailureAction Classify(
        bool captureIsOpen,
        double currentFramePosition,
        double frameCount,
        int retryCount,
        int maxRetries)
    {
        if (!captureIsOpen)
            return VideoReadFailureAction.CaptureError;

        if (frameCount > 0 && currentFramePosition >= frameCount - 1)
            return VideoReadFailureAction.EndOfStream;

        return retryCount < maxRetries
            ? VideoReadFailureAction.Retry
            : VideoReadFailureAction.ReadFailed;
    }
}

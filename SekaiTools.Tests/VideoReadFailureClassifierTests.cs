using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class VideoReadFailureClassifierTests
{
    [Fact]
    public void Classify_ReturnsEndOfStreamAtKnownVideoEnd()
    {
        var action = VideoReadFailureClassifier.Classify(true, 100, 100, 0, 2);

        Assert.Equal(VideoReadFailureAction.EndOfStream, action);
    }

    [Fact]
    public void Classify_RetriesTransientFailureBeforeLimit()
    {
        Assert.Equal(VideoReadFailureAction.Retry,
            VideoReadFailureClassifier.Classify(true, 50, 100, 0, 2));
        Assert.Equal(VideoReadFailureAction.Retry,
            VideoReadFailureClassifier.Classify(true, 50, 100, 1, 2));
        Assert.Equal(VideoReadFailureAction.ReadFailed,
            VideoReadFailureClassifier.Classify(true, 50, 100, 2, 2));
    }

    [Fact]
    public void Classify_ReturnsCaptureErrorWhenCaptureClosed()
    {
        var action = VideoReadFailureClassifier.Classify(false, 50, 100, 0, 2);

        Assert.Equal(VideoReadFailureAction.CaptureError, action);
    }
}

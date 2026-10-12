using System.Diagnostics;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class PreviewThrottleTests
{
    [Fact]
    public void FastProcessingProducesAtMostFivePreviewsPerSecond()
    {
        var throttle = new PreviewThrottle();
        var accepted = Enumerable.Range(0, 100)
            .Count(index => throttle.TryAcquire(index * Stopwatch.Frequency / 100));
        Assert.Equal(5, accepted);
    }

    [Fact]
    public void FirstPreviewAndPreviewAfterLongPauseAreImmediate()
    {
        var throttle = new PreviewThrottle();
        Assert.True(throttle.TryAcquire(0));
        Assert.False(throttle.TryAcquire(Stopwatch.Frequency / 10));
        Assert.True(throttle.TryAcquire(10 * Stopwatch.Frequency));
        Assert.False(throttle.TryAcquire(10 * Stopwatch.Frequency));
    }
}

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.Tests;

public class AdaptiveSearchSchedulerTests
{
    [Fact]
    public void ShouldSample_SkipsIntermediateSearchFrame()
    {
        using var scheduler = new AdaptiveSearchScheduler(2);

        Assert.True(scheduler.ShouldSample(10));
        scheduler.CompleteSample(10);
        Assert.False(scheduler.ShouldSample(11));
        Assert.True(scheduler.ShouldSample(12));
    }

    [Fact]
    public void TryGetPrevious_OnlyReturnsRememberedPreviousFrame()
    {
        using var source = new Mat(8, 8, DepthType.Cv8U, 3);
        source.SetTo(new MCvScalar(20, 30, 40));
        using var context = new FrameMatchContext();
        context.Update(source);
        using var scheduler = new AdaptiveSearchScheduler(2);
        scheduler.RememberSkipped(11);

        Assert.True(scheduler.TryGetPrevious(context, 11, out var previous, out var frameIndex));
        Assert.Equal(11, frameIndex);
        Assert.Same(context, previous);
        Assert.False(scheduler.TryGetPrevious(context, 10, out _, out _));
    }
}

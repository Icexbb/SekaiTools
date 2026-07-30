using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.Tests;

public class FiniteLookaheadTrackerTests
{
    [Fact]
    public void ShouldProbe_TriggersAfterConfiguredFrameWindow()
    {
        var tracker = new FiniteLookaheadTracker();

        Assert.False(tracker.ShouldProbe(2, 100, 30));
        Assert.False(tracker.ShouldProbe(2, 129, 30));
        Assert.True(tracker.ShouldProbe(2, 130, 30));
    }

    [Fact]
    public void Postpone_StartsANewProbeWindow()
    {
        var tracker = new FiniteLookaheadTracker();
        tracker.ShouldProbe(2, 100, 30);
        tracker.Postpone(130);

        Assert.False(tracker.ShouldProbe(2, 159, 30));
        Assert.True(tracker.ShouldProbe(2, 160, 30));
    }

    [Fact]
    public void ChangingTarget_ResetsFailureWindow()
    {
        var tracker = new FiniteLookaheadTracker();
        tracker.ShouldProbe(2, 100, 30);

        Assert.False(tracker.ShouldProbe(3, 200, 30));
    }
}

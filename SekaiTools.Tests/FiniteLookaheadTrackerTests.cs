using System.Text.Json;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class FiniteLookaheadTrackerTests
{
    [Fact]
    public void RepeatedFailuresBackOffButVisualChangeCanWakeProbeAfterCooldown()
    {
        var tracker = new FiniteLookaheadTracker();
        var before = new byte[128];
        var after = Enumerable.Repeat((byte)80, 128).ToArray();
        Assert.False(tracker.ShouldProbe(0, 0, 90, before));
        Assert.True(tracker.ShouldProbe(0, 90, 90, before));
        tracker.Postpone(90);
        Assert.True(tracker.ShouldProbe(0, 180, 90, before));
        tracker.Postpone(180);
        Assert.False(tracker.ShouldProbe(0, 270, 90, before));
        Assert.True(tracker.ShouldProbe(0, 360, 90, before));
        tracker.Postpone(360);
        Assert.False(tracker.ShouldProbe(0, 361, 90, after));
        var saved = JsonSerializer.Deserialize<LookaheadStateDto>(
            JsonSerializer.Serialize(tracker.CaptureState()))!;
        var restored = new FiniteLookaheadTracker();
        restored.RestoreState(saved);
        Assert.False(restored.ShouldProbe(0, 374, 90, after));
        Assert.True(restored.ShouldProbe(0, 375, 90, after));
        Assert.False(restored.ShouldProbe(0, 375, 90, after));
        Assert.True(tracker.ShouldProbe(0, 375, 90, after));
        Assert.False(VisualChangeEvidence.HasChanged(before, Enumerable.Repeat((byte)4, 128).ToArray()));
    }

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
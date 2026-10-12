using System.Text.Json;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class MatcherSearchStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundtripPreservesSamplingPhaseAndPendingBackcheck(bool skipped)
    {
        using var continuous = new AdaptiveSearchScheduler();
        continuous.CompleteSample(10);
        if (skipped) continuous.RememberSkipped(11);
        var checkpoint = Roundtrip(new DialogMatcherStateDto { SearchState = continuous.CaptureState() });
        using var resumed = new AdaptiveSearchScheduler();
        resumed.RestoreState(checkpoint.SearchState);
        using var previous = new FrameMatchContext();
        for (var frame = 11; frame <= 13; frame++)
            Assert.Equal(continuous.ShouldSample(frame), resumed.ShouldSample(frame));
        Assert.Equal(skipped, resumed.TryGetPrevious(previous, 11, out _, out _));
        Assert.False(resumed.TryGetPrevious(previous, 10, out _, out _));
        resumed.CompleteSample(12);
        Assert.False(resumed.TryGetPrevious(previous, 11, out _, out _));
    }

    [Fact]
    public void RoundtripPreservesLookaheadWindowAndTarget()
    {
        var continuous = new FiniteLookaheadTracker();
        continuous.ShouldProbe(2, 100, 30);
        continuous.Postpone(110);
        var checkpoint = Roundtrip(new BannerMatcherStateDto { LookaheadState = continuous.CaptureState() });
        var resumed = new FiniteLookaheadTracker();
        resumed.RestoreState(checkpoint.LookaheadState);
        Assert.False(resumed.ShouldProbe(2, 139, 30));
        Assert.True(resumed.ShouldProbe(2, 140, 30));
        Assert.False(resumed.ShouldProbe(3, 141, 30));
        Assert.True(resumed.ShouldProbe(3, 171, 30));
    }

    [Fact]
    public void LegacyStateResetsTransientStateWhenFieldsAreAbsent()
    {
        var checkpoint = JsonSerializer.Deserialize<MarkerMatcherStateDto>("{\"Status\":0}")!;
        using var scheduler = new AdaptiveSearchScheduler();
        scheduler.CompleteSample(10);
        scheduler.RememberSkipped(11);
        scheduler.RestoreState(checkpoint.SearchState);
        Assert.True(scheduler.ShouldSample(11));
        using var previous = new FrameMatchContext();
        Assert.False(scheduler.TryGetPrevious(previous, 11, out _, out _));
        var tracker = new FiniteLookaheadTracker();
        tracker.ShouldProbe(2, 100, 30);
        tracker.RestoreState(checkpoint.LookaheadState);
        Assert.False(tracker.ShouldProbe(2, 140, 30));
    }

    private static T Roundtrip<T>(T state) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(state))!;
}

using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiTools.Tests;

public class MatcherThresholdTests
{
    [Fact]
    public void EffectiveThreshold_UsesLowerExitThresholdWhileTracking()
    {
        var matcher = new ThresholdMatcher();

        Assert.Equal(0.8, matcher.Threshold(0.8, false), 6);
        Assert.Equal(0.752, matcher.Threshold(0.8, true), 6);
    }

    [Fact]
    public void MarkMissing_CompletesTargetAndRecordsDiagnostic()
    {
        var frameSet = new TestFrameSet();
        var matcher = new ThresholdMatcher([frameSet]);

        matcher.Missing(0, 120, "后续目标已命中");

        Assert.True(frameSet.Finished);
        var diagnostic = Assert.Single(matcher.Diagnostics);
        Assert.Equal(0, diagnostic.TargetIndex);
        Assert.Equal(120, diagnostic.FrameIndex);
        Assert.Equal("后续目标已命中", diagnostic.Reason);
    }

    private sealed class ThresholdMatcher(List<TestFrameSet>? sets = null)
        : MatcherStateMachine<TestFrameSet>(sets ?? [], 10)
    {
        public double Threshold(double value, bool tracking)
        {
            return EffectiveThreshold(value, tracking);
        }

        public void Missing(int index, int frameIndex, string reason)
        {
            MarkMissing(index, frameIndex, reason);
        }
    }

    private sealed class TestFrameSet : BaseFrameSet
    {
        public override bool IsEmpty() => true;
        public override IProcessFrame Start() => throw new NotSupportedException();
        public override IProcessFrame End() => throw new NotSupportedException();
    }
}

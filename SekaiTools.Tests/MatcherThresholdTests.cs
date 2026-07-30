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

    private sealed class ThresholdMatcher() : MatcherStateMachine<TestFrameSet>([], 10)
    {
        public double Threshold(double value, bool tracking)
        {
            return EffectiveThreshold(value, tracking);
        }
    }

    private sealed class TestFrameSet : BaseFrameSet
    {
        public override bool IsEmpty() => true;
        public override IProcessFrame Start() => throw new NotSupportedException();
        public override IProcessFrame End() => throw new NotSupportedException();
    }
}

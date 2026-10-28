using System.Text.Json;
using SekaiToolsCore.Process;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiTools.Tests;

public class MatcherThresholdTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FallbackUsesElapsedFramesAndSurvivesRestore(int samplingInterval)
    {
        var matcher = new ThresholdMatcher();
        for (var frame = 100; frame < 110; frame += samplingInterval)
            Assert.False(matcher.Fail(frame));
        var restored = new ThresholdMatcher();
        restored.Load(JsonSerializer.Deserialize<DialogMatcherStateDto>(JsonSerializer.Serialize(matcher.Save()))!);
        Assert.True(matcher.Fail(110));
        Assert.True(restored.Fail(110));
        Assert.False(matcher.Fail(110));
        Assert.False(matcher.Fail(110));
    }

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
        public bool Fail(int frame) => TryEnterFallback(frame);
        public DialogMatcherStateDto Save()
        {
            var (failures, target, fallback) = SaveFallbackState();
            return new DialogMatcherStateDto
            {
                ConsecutiveFailures = failures,
                LastFailedIndex = target,
                UseFallbackThreshold = fallback,
                LastFailureFrame = LastFailureFrame
            };
        }
        public void Load(DialogMatcherStateDto state) =>
            RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold, state.LastFailureFrame);
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
        public override bool IsEmpty()
        {
            return true;
        }

        public override IProcessFrame Start()
        {
            throw new NotSupportedException();
        }

        public override IProcessFrame End()
        {
            throw new NotSupportedException();
        }
    }
}

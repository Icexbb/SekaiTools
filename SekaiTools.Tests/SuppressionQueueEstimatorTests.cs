using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionQueueEstimatorTests
{
    private static VideoSuppressionOptions Options(int frames = 1000, int height = 1080) => new("source", "sub.ass", "output", new(), frames, SourceHeight: height);
    private static SuppressionTimingSample Running() => new(Options(), new(100, 1000, 10, VideoSuppressionState.Running, "", ElapsedSeconds: 10));
    [Fact]
    public void SpeedSettingsSubtitleModeAndInvalidRatesAreNotMixed()
    {
        var sample = Running();
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample, [Options() with { SourceSubtitle = "" }], []));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample, [Options() with { EncodingSettings = new(Speed: VideoEncodingSpeedPreset.Slow) }], []));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample with { Progress = sample.Progress with { FramesPerSecond = double.NaN } }, [], []));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample with { Progress = sample.Progress with { State = VideoSuppressionState.Cancelling } }, [], []));
    }

    [Fact]
    public void AddsCurrentRemainderAndAllPendingFrames()
    {
        Assert.Equal(TimeSpan.FromSeconds(290), SuppressionQueueEstimator.EstimateRemaining(Running(), [Options(), Options()], []));
        Assert.Equal(TimeSpan.FromSeconds(90), SuppressionQueueEstimator.EstimateRemaining(Running(), [], []));
    }
    [Fact]
    public void UnknownOrDifferentInputsDoNotProduceFalseCompletionTime()
    {
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(Running(), [Options(height: 2160)], []));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(Running(), [Options(frames: 0)], []));
        var sample = Running();
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample with { Progress = sample.Progress with { ElapsedSeconds = 1 } }, [], []));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(sample with { Progress = sample.Progress with { IsFinalizing = true } }, [], []));
    }
    [Fact]
    public void SuccessfulHistorySupportsLaterMatchingTasksButFailedHistoryDoesNot()
    {
        var finished = Running() with { Progress = new(1000, 1000, 10, VideoSuppressionState.Completed, "", ElapsedSeconds: 100) };
        Assert.Equal(TimeSpan.FromSeconds(100), SuppressionQueueEstimator.EstimateRemaining(null, [Options()], [finished]));
        Assert.Null(SuppressionQueueEstimator.EstimateRemaining(null, [Options()], [finished with { Progress = finished.Progress with { State = VideoSuppressionState.Failed } }]));
        Assert.Equal(TimeSpan.Zero, SuppressionQueueEstimator.EstimateRemaining(null, [], []));
    }
}

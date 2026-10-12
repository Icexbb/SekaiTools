using SekaiToolsCore.Process.Performance;

namespace SekaiTools.Tests;

public class ProcessingPerformanceMetricsTests
{
    [Fact]
    public void AverageUsesWallTimeWhenStagesOverlap()
    {
        var metrics = new ProcessingPerformanceMetrics();
        metrics.Record(ProcessingStage.Decode, TimeSpan.FromMilliseconds(10));
        metrics.Record(ProcessingStage.Match, TimeSpan.FromMilliseconds(10));
        metrics.Record(ProcessingStage.ReadWait, TimeSpan.FromMilliseconds(4));
        metrics.Record(ProcessingStage.Preview, TimeSpan.FromMilliseconds(1));
        metrics.RecordElapsed(TimeSpan.FromMilliseconds(12));
        metrics.RecordFrame();
        Assert.Equal(12, metrics.Snapshot().AverageMillisecondsPerFrame);
        Assert.Equal(TimeSpan.FromMilliseconds(20), metrics.Snapshot().MeasuredTime);
        Assert.Equal(TimeSpan.FromMilliseconds(4), metrics.Snapshot().ReadWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(1), metrics.Snapshot().PreviewTime);
        metrics.Reset();
        Assert.Equal(TimeSpan.Zero, metrics.Snapshot().ElapsedTime);
        Assert.Equal(TimeSpan.Zero, metrics.Snapshot().ReadWaitTime);
        Assert.Equal(TimeSpan.Zero, metrics.Snapshot().PreviewTime);
    }

    [Fact]
    public void Snapshot_AggregatesStagesAndFrames()
    {
        var metrics = new ProcessingPerformanceMetrics();
        metrics.Record(ProcessingStage.Decode, TimeSpan.FromMilliseconds(2));
        metrics.Record(ProcessingStage.Preprocess, TimeSpan.FromMilliseconds(3));
        metrics.Record(ProcessingStage.Match, TimeSpan.FromMilliseconds(5));
        metrics.RecordFrame();
        metrics.RecordFrame();

        var snapshot = metrics.Snapshot();

        Assert.Equal(2, snapshot.FrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(2), snapshot.DecodeTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), snapshot.PreprocessTime);
        Assert.Equal(TimeSpan.FromMilliseconds(5), snapshot.MatchTime);
        Assert.Equal(5, snapshot.AverageMillisecondsPerFrame);
    }

    [Fact]
    public void Reset_ClearsAllMeasurements()
    {
        var metrics = new ProcessingPerformanceMetrics();
        metrics.Record(ProcessingStage.Match, TimeSpan.FromSeconds(1));
        metrics.RecordFrame();

        metrics.Reset();

        Assert.Equal(new ProcessingPerformanceSnapshot(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            metrics.Snapshot());
    }
}

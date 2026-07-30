using SekaiToolsCore.Process.Performance;

namespace SekaiTools.Tests;

public class ProcessingPerformanceMetricsTests
{
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

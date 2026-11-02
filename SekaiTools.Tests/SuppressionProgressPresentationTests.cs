using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionProgressPresentationTests
{
    [Fact]
    public void DurationsDoNotWrapAfterTwentyFourHours()
    {
        var progress = new VideoSuppressionProgress(100, 1000, 1, VideoSuppressionState.Running, "", ElapsedSeconds: 90061);
        Assert.Equal("已用 25:01:01", progress.ElapsedDisplay);
    }

    [Fact]
    public void EstimateWaitsForEnoughSamplesAndKnownFrameCount()
    {
        var progress = new VideoSuppressionProgress(20, 1000, 10, VideoSuppressionState.Running, "", ElapsedSeconds: 2);
        Assert.Contains("估算中", progress.RemainingDisplay);
        Assert.Contains("估算中", (progress with { ProcessedFrames = 100, ElapsedSeconds = 10, TotalFrames = 0 }).RemainingDisplay);
        Assert.Contains("00:01:30", (progress with { ProcessedFrames = 100, ElapsedSeconds = 10 }).RemainingDisplay);
        Assert.Contains("00:00:00", (progress with { ProcessedFrames = 1100, ElapsedSeconds = 10 }).RemainingDisplay);
    }

    [Theory]
    [InlineData(VideoSuppressionState.Completed)]
    [InlineData(VideoSuppressionState.Cancelled)]
    [InlineData(VideoSuppressionState.Failed)]
    public void TerminalStatesDoNotPromiseRemainingTime(VideoSuppressionState state)
    {
        var progress = new VideoSuppressionProgress(100, 1000, 10, state, "", ElapsedSeconds: 10);
        Assert.Empty(progress.RemainingDisplay);
        Assert.Contains("00:00:10", progress.ElapsedDisplay);
    }

    [Fact]
    public void FinalizingIsDifferentFromEncoding()
    {
        var progress = new VideoSuppressionProgress(100, 100, 10, VideoSuppressionState.Running, "", ElapsedSeconds: 10, IsFinalizing: true);
        Assert.Equal("正在完成输出", progress.Status);
        Assert.Empty(progress.RemainingDisplay);
    }
}

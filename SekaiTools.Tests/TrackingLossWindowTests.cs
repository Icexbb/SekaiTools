using System.Text.Json;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class TrackingLossWindowTests
{
    [Theory]
    [InlineData(30, 2)]
    [InlineData(60, 3)]
    public void RestoreKeepsDeadlineAndRecoveryResetsWindow(double fps, int limit)
    {
        var loss = new TrackingLossWindow(fps);
        Assert.True(loss.ShouldWait(10));
        var state = JsonSerializer.Deserialize<BannerMatcherStateDto>(JsonSerializer.Serialize(
            new BannerMatcherStateDto { PendingLossFrame = loss.FirstMissingFrame }))!;
        var restored = new TrackingLossWindow(fps);
        restored.Restore(state.PendingLossFrame);
        for (var frame = 11; frame <= 10 + limit; frame++)
            Assert.Equal(loss.ShouldWait(frame), restored.ShouldWait(frame));
        Assert.False(restored.ShouldWait(10 + limit));
        restored.Clear();
        Assert.True(restored.ShouldWait(100));
        restored.Restore(JsonSerializer.Deserialize<MarkerMatcherStateDto>("{}")!.PendingLossFrame);
        Assert.Null(restored.FirstMissingFrame);
    }
}

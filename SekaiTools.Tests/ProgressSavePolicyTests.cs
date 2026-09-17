using SekaiToolsCore.Process;
using Xunit;

namespace SekaiTools.Tests;

public sealed class ProgressSavePolicyTests
{
    [Fact]
    public void SaveIntervalGrowsWithProcessedFrames()
    {
        Assert.Equal(300, ProgressSavePolicy.GetNextFrame(0));
        Assert.Equal(600, ProgressSavePolicy.GetNextFrame(300));
        Assert.Equal(1200, ProgressSavePolicy.GetNextFrame(600));
        Assert.Equal(240_000, ProgressSavePolicy.GetNextFrame(120_000));
    }

    [Fact]
    public void MillionFrameRunUsesLogarithmicCheckpointCount()
    {
        var frame = 0;
        var checkpoints = 0;
        while (frame < 1_000_000)
        {
            frame = ProgressSavePolicy.GetNextFrame(frame);
            checkpoints++;
        }

        Assert.InRange(checkpoints, 1, 32);
    }

    [Fact]
    public void CompletionSaveIsDebounced()
    {
        Assert.False(ProgressSavePolicy.ShouldSave(110, 300, 100, frameSetJustCompleted: true));
        Assert.False(ProgressSavePolicy.ShouldSave(130, 300, 100, frameSetJustCompleted: true));
        Assert.True(ProgressSavePolicy.ShouldSave(40, 300, 0, frameSetJustCompleted: true));
        Assert.True(ProgressSavePolicy.ShouldSave(300, 300, 100, frameSetJustCompleted: false));
    }
}

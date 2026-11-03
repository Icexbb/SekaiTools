using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class FrameRateTimecodeTests
{
    [Fact]
    public void CheckpointBlocksReuseImmutablePrefixAndInvalidateAfterCorrectionOrRestore()
    {
        var rate = new FrameRate(30);
        rate.RestoreTimecodes(Enumerable.Range(0, 5000).Select(i => i * 40));
        var first = rate.CaptureTimecodeBlocks();
        rate.RecordTimecode(5000, 200000);
        var second = rate.CaptureTimecodeBlocks();
        Assert.Same(first[0], second[0]);
        Assert.NotSame(first[1], second[1]);
        Assert.Equal(5000, first.Sum(b => b.Length));
        Assert.Equal(rate.ExportTimecodes(), second.SelectMany(b => b));
        rate.RecordTimecode(100, 4001);
        var corrected = rate.CaptureTimecodeBlocks();
        Assert.NotSame(first[0], corrected[0]);
        Assert.Equal(4000, first[0][100]);
        Assert.Equal(4001, corrected[0][100]);
        rate.RestoreTimecodes(Enumerable.Range(0, 5000).Select(i => i * 50));
        Assert.NotSame(corrected[0], rate.CaptureTimecodeBlocks()[0]);
        Assert.Equal(5000, rate.CaptureTimecodeBlocks()[0][100]);
    }

    [Fact]
    public void RecordTimecode_UsesExplicitVariableFrameTimes()
    {
        var frameRate = new FrameRate(30);

        Assert.True(frameRate.RecordTimecode(0, 0));
        Assert.True(frameRate.RecordTimecode(1, 40));
        Assert.True(frameRate.RecordTimecode(2, 95));

        Assert.True(frameRate.IsVfr());
        Assert.Equal(40, frameRate.TimeAtFrame(1).Milliseconds);
        Assert.Equal(95, frameRate.TimeAtFrame(2).Milliseconds);
        Assert.Equal(128, frameRate.TimeAtFrame(3).Milliseconds);
    }

    [Fact]
    public void RestoreTimecodes_PreservesTimingAcrossResume()
    {
        var source = new FrameRate(30);
        source.RecordTimecode(0, 0);
        source.RecordTimecode(1, 42);
        source.RecordTimecode(2, 90);
        var restored = new FrameRate(30);

        restored.RestoreTimecodes(source.ExportTimecodes());

        Assert.Equal(source.ExportTimecodes(), restored.ExportTimecodes());
        Assert.Equal(90, restored.TimeAtFrame(2).Milliseconds);
    }

    [Fact]
    public void RecordTimecode_RejectsUnavailableNonInitialTimestamp()
    {
        var frameRate = new FrameRate(30);

        Assert.False(frameRate.RecordTimecode(1, 0));
        Assert.False(frameRate.IsVfr());
    }
}
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class FrameRateTimecodeTests
{
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

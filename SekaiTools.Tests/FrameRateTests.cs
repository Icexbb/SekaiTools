using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class FrameRateTests
{
    [Theory]
    [InlineData(24, 24, 1000)]
    [InlineData(25, 50, 2000)]
    [InlineData(60, 90, 1500)]
    public void 固定帧率能够换算帧时间(double fps, int frame, int expectedMilliseconds)
    {
        var frameRate = new FrameRate(fps);

        Assert.Equal(expectedMilliseconds, frameRate.TimeAtFrame(frame).Milliseconds);
        Assert.Equal(frame, frameRate.FrameAtTime(expectedMilliseconds));
    }

    [Fact]
    public void 字幕时间按Ass和Srt格式输出()
    {
        var time = new SubtitleTime(3_723_456);

        Assert.Equal("1:02:03.46", time.GetAssFormatted());
        Assert.Equal("01:02:03,460", time.GetSrtFormatted());
        Assert.Equal("1:02:03.456", time.GetAssFormatted(true));
    }
}
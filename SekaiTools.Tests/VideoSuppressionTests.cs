using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class VideoSuppressionTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 100, 0.5)]
    [InlineData(150, 100, 1)]
    public void 压制进度始终处于有效范围(int processed, int total, double expected)
    {
        var progress = new VideoSuppressionProgress(processed, total, 0, true, "");

        Assert.Equal(expected, progress.Fraction);
    }

    [Fact]
    public void 简单编码参数包含用户设置的质量值()
    {
        var parameters = new X264Params { Crf = 18 };

        Assert.Contains("crf=18", parameters.GetSimpleX264Params());
    }

    [Fact]
    public void 未选择字幕时压制选项有效()
    {
        var sourceVideo = Path.GetTempFileName();
        try
        {
            var options = new VideoSuppressionOptions(sourceVideo, "", "output.mp4", "crf=21");

            options.Validate();
        }
        finally
        {
            File.Delete(sourceVideo);
        }
    }

    [Fact]
    public void 已选择的字幕必须存在()
    {
        var sourceVideo = Path.GetTempFileName();
        try
        {
            var options = new VideoSuppressionOptions(sourceVideo, "missing.ass", "output.mp4", "crf=21");

            Assert.Throws<FileNotFoundException>(options.Validate);
        }
        finally
        {
            File.Delete(sourceVideo);
        }
    }
}

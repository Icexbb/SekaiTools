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
}

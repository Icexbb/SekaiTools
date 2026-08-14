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

    [Fact]
    public void 音频探测保留全部兼容音轨()
    {
        const string log = """
                               Stream #0:1[0x2](jpn): Audio: aac (LC), 48000 Hz, stereo
                               Stream #0:2(chi): Audio: mp3, 48000 Hz, stereo
                           """;

        var plan = FfmpegAudioInspector.Parse(log);
        var arguments = VideoSuppressor.BuildFfmpegArguments(CreateOptions(), plan);

        Assert.Equal(2, plan.StreamCount);
        Assert.True(plan.CopyAudio);
        AssertArgumentPair(arguments, "-map", "1:a?");
        AssertArgumentPair(arguments, "-c:a", "copy");
    }

    [Fact]
    public void 无音轨视频使用可选音频映射()
    {
        var plan = FfmpegAudioInspector.Parse("Stream #0:0: Video: h264, yuv420p");
        var arguments = VideoSuppressor.BuildFfmpegArguments(CreateOptions(), plan);

        Assert.Equal(0, plan.StreamCount);
        AssertArgumentPair(arguments, "-map", "1:a?");
    }

    [Theory]
    [InlineData("opus")]
    [InlineData("vorbis")]
    [InlineData("pcm_s16le")]
    public void Mp4不兼容音频统一转为Aac(string codec)
    {
        var plan = FfmpegAudioPlan.FromCodecs(["aac", codec]);
        var arguments = VideoSuppressor.BuildFfmpegArguments(CreateOptions(), plan);

        Assert.Equal(2, plan.StreamCount);
        Assert.False(plan.CopyAudio);
        AssertArgumentPair(arguments, "-c:a", "aac");
        AssertArgumentPair(arguments, "-b:a", "192k");
    }

    private static VideoSuppressionOptions CreateOptions()
    {
        return new VideoSuppressionOptions("input.mkv", "", "output.mp4", "crf=21");
    }

    private static void AssertArgumentPair(IReadOnlyList<string> arguments, string name, string value)
    {
        var found = arguments
            .Zip(arguments.Skip(1))
            .Any(pair => pair.First == name && pair.Second == value);
        Assert.True(found, $"未找到参数组合 {name} {value}");
    }
}

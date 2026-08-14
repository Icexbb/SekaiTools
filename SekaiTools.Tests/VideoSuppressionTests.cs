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
        var progress = new VideoSuppressionProgress(
            processed, total, 0, VideoSuppressionState.Running, "");

        Assert.Equal(expected, progress.Fraction);
    }

    [Theory]
    [InlineData(VideoSuppressionState.Preparing, true)]
    [InlineData(VideoSuppressionState.Running, true)]
    [InlineData(VideoSuppressionState.Cancelling, true)]
    [InlineData(VideoSuppressionState.Completed, false)]
    [InlineData(VideoSuppressionState.Cancelled, false)]
    [InlineData(VideoSuppressionState.Failed, false)]
    public void 压制状态准确标识活动任务(VideoSuppressionState state, bool expectedRunning)
    {
        var progress = new VideoSuppressionProgress(0, 0, 0, state, "");

        Assert.Equal(expectedRunning, progress.Running);
    }

    [Fact]
    public async Task 输入校验失败时发布准备和失败状态()
    {
        using var suppressor = new VideoSuppressor(new UnusedMediaResourceProvider());
        var states = new List<VideoSuppressionState>();
        suppressor.ProgressChanged += progress => states.Add(progress.State);
        var options = new VideoSuppressionOptions(
            "missing.mp4", "", "output.mp4", new X264EncodingSettings());

        await Assert.ThrowsAsync<FileNotFoundException>(() => suppressor.SuppressAsync(options));

        Assert.Equal([VideoSuppressionState.Preparing, VideoSuppressionState.Failed], states);
    }

    [Theory]
    [InlineData(VideoQualityPreset.HighQuality, 18)]
    [InlineData(VideoQualityPreset.Balanced, 21)]
    [InlineData(VideoQualityPreset.Compact, 25)]
    [InlineData(VideoQualityPreset.Custom, 23)]
    public void 画质预设映射到正确Crf(VideoQualityPreset preset, int expectedCrf)
    {
        var settings = new X264EncodingSettings(preset, CustomCrf: 23);

        Assert.Equal(expectedCrf, settings.Crf);
    }

    [Theory]
    [InlineData(VideoEncodingSpeedPreset.Fast, "fast")]
    [InlineData(VideoEncodingSpeedPreset.Balanced, "medium")]
    [InlineData(VideoEncodingSpeedPreset.Slow, "veryslow")]
    public void 速度预设映射到编码器参数(VideoEncodingSpeedPreset preset, string expectedPreset)
    {
        var settings = new X264EncodingSettings(Speed: preset);

        Assert.Equal(expectedPreset, settings.FfmpegPreset);
    }

    [Fact]
    public void 自定义Crf超出范围时拒绝编码()
    {
        var settings = new X264EncodingSettings(VideoQualityPreset.Custom, CustomCrf: 52);

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void Ffmpeg参数包含所选画质和速度预设()
    {
        var settings = new X264EncodingSettings(
            VideoQualityPreset.Custom, VideoEncodingSpeedPreset.Slow, 19);
        var options = new VideoSuppressionOptions("input.mkv", "", "output.mp4", settings);
        var arguments = VideoSuppressor.BuildFfmpegArguments(
            options, FfmpegAudioPlan.FromCodecs([]));

        AssertArgumentPair(arguments, "-preset", "veryslow");
        AssertArgumentPair(arguments, "-crf", "19");
    }

    [Fact]
    public void 未选择字幕时压制选项有效()
    {
        var sourceVideo = Path.GetTempFileName();
        try
        {
            var options = new VideoSuppressionOptions(
                sourceVideo, "", "output.mp4", new X264EncodingSettings());

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
            var options = new VideoSuppressionOptions(
                sourceVideo, "missing.ass", "output.mp4", new X264EncodingSettings());

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
        Assert.DoesNotContain("-nostats", arguments);
        Assert.DoesNotContain("-progress", arguments);
    }

    [Fact]
    public void 从原始Ffmpeg日志解析进度信息()
    {
        const string log =
            "frame=  125 fps=48.25 q=24.0 size=1024KiB time=00:00:05.20 bitrate=1613.2kbits/s speed=1.20x";

        var parsed = VideoSuppressor.TryParseFfmpegProgress(log, out var progress);

        Assert.True(parsed);
        Assert.Equal(125, progress.Frame);
        Assert.Equal(48.25, progress.FramesPerSecond);
        Assert.Equal("1613.2kbits/s", progress.Bitrate);
        Assert.Equal("1.20x", progress.Speed);
        Assert.Equal("1024KiB", progress.OutputSize);
        Assert.Equal("00:00:05.20", progress.OutputTime);
    }

    [Fact]
    public void 普通Ffmpeg日志不会被识别为进度行()
    {
        Assert.False(VideoSuppressor.TryParseFfmpegProgress(
            "Stream #0:0: Video: h264", out _));
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

    [Fact]
    public void 未确认时拒绝覆盖已有输出文件()
    {
        var sourceVideo = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            var options = new VideoSuppressionOptions(
                sourceVideo, "", outputPath, new X264EncodingSettings(), OverwriteExisting: false);

            Assert.Throws<IOException>(options.Validate);
            (options with { OverwriteExisting = true }).Validate();
        }
        finally
        {
            File.Delete(sourceVideo);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void 输出路径不能与源视频相同()
    {
        var sourceVideo = Path.GetTempFileName();
        try
        {
            var options = new VideoSuppressionOptions(
                sourceVideo, "", sourceVideo, new X264EncodingSettings(), OverwriteExisting: true);

            Assert.Throws<ArgumentException>(options.Validate);
        }
        finally
        {
            File.Delete(sourceVideo);
        }
    }

    [Fact]
    public void 未提交的压制临时文件会被清理()
    {
        var targetPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        string temporaryPath;
        using (var transaction = new VideoOutputTransaction(targetPath, false))
        {
            temporaryPath = transaction.TemporaryPath;
            File.WriteAllText(temporaryPath, "partial");
        }

        Assert.False(File.Exists(temporaryPath));
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void 提交压制结果时替换已确认覆盖的文件()
    {
        var targetPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(targetPath, "old");
            using var transaction = new VideoOutputTransaction(targetPath, true);
            File.WriteAllText(transaction.TemporaryPath, "new");

            transaction.Commit();

            Assert.Equal("new", File.ReadAllText(targetPath));
            Assert.False(File.Exists(transaction.TemporaryPath));
        }
        finally
        {
            File.Delete(targetPath);
        }
    }

    private static VideoSuppressionOptions CreateOptions()
    {
        return new VideoSuppressionOptions(
            "input.mkv", "", "output.mp4", new X264EncodingSettings());
    }

    private static void AssertArgumentPair(IReadOnlyList<string> arguments, string name, string value)
    {
        var found = arguments
            .Zip(arguments.Skip(1))
            .Any(pair => pair.First == name && pair.Second == value);
        Assert.True(found, $"未找到参数组合 {name} {value}");
    }

    private sealed class UnusedMediaResourceProvider : IMediaResourceProvider
    {
        public string GetVapourSynthResourcePath(string fileName)
        {
            throw new InvalidOperationException("输入校验失败时不应访问媒体资源");
        }
    }
}

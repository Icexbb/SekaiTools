namespace SekaiToolsMedia;

public enum VideoQualityPreset
{
    HighQuality,
    Balanced,
    Compact,
    Custom
}

public enum VideoEncodingSpeedPreset
{
    Fast,
    Balanced,
    Slow
}

public sealed record X264EncodingSettings(
    VideoQualityPreset Quality = VideoQualityPreset.Balanced,
    VideoEncodingSpeedPreset Speed = VideoEncodingSpeedPreset.Balanced,
    int CustomCrf = 21,
    int Threads = 0)
{
    public int Crf => Quality switch
    {
        VideoQualityPreset.HighQuality => 18,
        VideoQualityPreset.Balanced => 21,
        VideoQualityPreset.Compact => 25,
        VideoQualityPreset.Custom => CustomCrf,
        _ => throw new ArgumentOutOfRangeException(nameof(Quality), Quality, null)
    };

    public string FfmpegPreset => Speed switch
    {
        VideoEncodingSpeedPreset.Fast => "fast",
        VideoEncodingSpeedPreset.Balanced => "medium",
        VideoEncodingSpeedPreset.Slow => "veryslow",
        _ => throw new ArgumentOutOfRangeException(nameof(Speed), Speed, null)
    };

    public void Validate()
    {
        if (Threads is < 0 or > 128)
            throw new ArgumentOutOfRangeException(nameof(Threads), Threads, "编码线程数必须处于 0 到 128 之间");
        if (Quality == VideoQualityPreset.Custom && CustomCrf is < 0 or > 51)
            throw new ArgumentOutOfRangeException(nameof(CustomCrf), CustomCrf, "CRF 必须处于 0 到 51 之间");

        _ = Crf;
        _ = FfmpegPreset;
    }

    // More frame threads help large frames keep the CPU busy. Small frames retain
    // x264's automatic choice, where extra threads can cost more than they save.
    internal int GetThreadCount(int sourceHeight, int processorCount)
    {
        if (Threads > 0) return Threads;
        if (sourceHeight < 1440 || processorCount <= 1) return 0;
        return (int)Math.Min(Math.Min((long)processorCount * 2, 128), sourceHeight / 32);
    }
}

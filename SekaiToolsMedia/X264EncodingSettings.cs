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
    int CustomCrf = 21)
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
        if (Quality == VideoQualityPreset.Custom && CustomCrf is < 0 or > 51)
            throw new ArgumentOutOfRangeException(nameof(CustomCrf), CustomCrf, "CRF 必须处于 0 到 51 之间");

        _ = Crf;
        _ = FfmpegPreset;
    }
}

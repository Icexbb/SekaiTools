namespace SekaiToolsMedia;

public sealed record VideoSuppressionOptions(
    string SourceVideo,
    string SourceSubtitle,
    string OutputPath,
    string X264Parameters,
    int SourceFrameCount = 0)
{
    public void Validate()
    {
        if (!File.Exists(SourceVideo))
            throw new FileNotFoundException("源视频不存在", SourceVideo);
        if (!string.IsNullOrWhiteSpace(SourceSubtitle) && !File.Exists(SourceSubtitle))
            throw new FileNotFoundException("字幕文件不存在", SourceSubtitle);
        if (string.IsNullOrWhiteSpace(OutputPath))
            throw new ArgumentException("输出路径不能为空", nameof(OutputPath));
    }
}

public sealed record VideoSuppressionProgress(
    int ProcessedFrames,
    int TotalFrames,
    double FramesPerSecond,
    bool Running,
    string Status)
{
    public double Fraction => TotalFrames > 0
        ? Math.Clamp((double)ProcessedFrames / TotalFrames, 0, 1)
        : 0;
}

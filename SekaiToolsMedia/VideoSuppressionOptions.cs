namespace SekaiToolsMedia;

public sealed record VideoSuppressionOptions(
    string SourceVideo,
    string SourceSubtitle,
    string OutputPath,
    X264EncodingSettings EncodingSettings,
    int SourceFrameCount = 0,
    bool OverwriteExisting = false)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(EncodingSettings);
        EncodingSettings.Validate();
        if (!File.Exists(SourceVideo))
            throw new FileNotFoundException("源视频不存在", SourceVideo);
        if (!string.IsNullOrWhiteSpace(SourceSubtitle) && !File.Exists(SourceSubtitle))
            throw new FileNotFoundException("字幕文件不存在", SourceSubtitle);
        if (string.IsNullOrWhiteSpace(OutputPath))
            throw new ArgumentException("输出路径不能为空", nameof(OutputPath));

        var sourcePath = Path.GetFullPath(SourceVideo);
        var outputPath = Path.GetFullPath(OutputPath);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(sourcePath, outputPath, pathComparison))
            throw new ArgumentException("输出路径不能与源视频相同", nameof(OutputPath));

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (outputDirectory == null || !Directory.Exists(outputDirectory))
            throw new DirectoryNotFoundException($"输出目录不存在: {outputDirectory}");
        if (File.Exists(outputPath) && !OverwriteExisting)
            throw new IOException($"输出文件已存在: {outputPath}");
    }
}

public enum VideoSuppressionState
{
    Idle,
    Preparing,
    Running,
    Cancelling,
    Completed,
    Cancelled,
    Failed
}

public sealed record VideoSuppressionProgress(
    int ProcessedFrames,
    int TotalFrames,
    double FramesPerSecond,
    VideoSuppressionState State,
    string Status,
    string Bitrate = "",
    string Speed = "",
    string OutputSize = "",
    string OutputTime = "")
{
    public bool Running => State is VideoSuppressionState.Preparing
        or VideoSuppressionState.Running
        or VideoSuppressionState.Cancelling;

    public double Fraction => TotalFrames > 0
        ? Math.Clamp((double)ProcessedFrames / TotalFrames, 0, 1)
        : 0;
}

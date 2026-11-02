namespace SekaiToolsMedia;

public sealed record VideoSuppressionOptions(
    string SourceVideo,
    string SourceSubtitle,
    string OutputPath,
    X264EncodingSettings EncodingSettings,
    int SourceFrameCount = 0,
    bool OverwriteExisting = false,
    int SourceHeight = 0)
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
    string Log,
    string Bitrate = "",
    string Speed = "",
    string OutputSize = "",
    string OutputTime = "",
    double ElapsedSeconds = 0,
    bool IsFinalizing = false)
{
    public string Status => State switch
    {
        VideoSuppressionState.Idle => "等待中",
        VideoSuppressionState.Preparing => "正在分析媒体",
        VideoSuppressionState.Running => IsFinalizing ? "正在完成输出" : "正在编码",
        VideoSuppressionState.Cancelling => "正在取消",
        VideoSuppressionState.Completed => "已完成",
        VideoSuppressionState.Cancelled => "已取消",
        VideoSuppressionState.Failed => "失败",
        _ => State.ToString()
    };

    public string ElapsedDisplay => $"已用 {FormatDuration(ElapsedSeconds)}";
    public string RemainingDisplay => State != VideoSuppressionState.Running || IsFinalizing ? "" :
        ElapsedSeconds < 5 || ProcessedFrames < 30 || FramesPerSecond <= 0 || TotalFrames <= 0
        ? "剩余时间估算中…"
        : $"预计剩余 {FormatDuration(Math.Clamp((TotalFrames - ProcessedFrames) / FramesPerSecond, 0, 31536000))}";
    private static string FormatDuration(double seconds)
    {
        var time = TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 31536000) : 0);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
    public string OutputDisplay => string.IsNullOrWhiteSpace(OutputSize) ? "输出大小待统计" : $"已输出 {OutputSize} · {Speed}";

    public bool Running => State is VideoSuppressionState.Preparing
        or VideoSuppressionState.Running
        or VideoSuppressionState.Cancelling;

    public double Fraction => TotalFrames > 0
        ? Math.Clamp((double)ProcessedFrames / TotalFrames, 0, 1)
        : 0;
}

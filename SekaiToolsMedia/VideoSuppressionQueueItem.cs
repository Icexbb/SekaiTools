namespace SekaiToolsMedia;

public sealed record VideoSuppressionQueueItem(
    string SourceVideo,
    string SourceSubtitle,
    string OutputPath)
{
    public string DisplayName => Path.GetFileName(SourceVideo);

    public string SubtitleSummary => string.IsNullOrWhiteSpace(SourceSubtitle)
        ? "未匹配字幕，仅转码"
        : $"字幕：{Path.GetFileName(SourceSubtitle)}";

    public static VideoSuppressionQueueItem Create(string sourceVideo)
    {
        if (string.IsNullOrWhiteSpace(sourceVideo))
            throw new ArgumentException("源视频路径不能为空", nameof(sourceVideo));

        var fullSourcePath = Path.GetFullPath(sourceVideo);
        var guessedSubtitle = Path.ChangeExtension(fullSourcePath, ".ass");
        var outputPath = Path.Combine(
            Path.GetDirectoryName(fullSourcePath)!,
            $"[STVS]{Path.GetFileNameWithoutExtension(fullSourcePath)}.mp4");
        return new VideoSuppressionQueueItem(
            fullSourcePath,
            File.Exists(guessedSubtitle) ? guessedSubtitle : "",
            outputPath);
    }
}

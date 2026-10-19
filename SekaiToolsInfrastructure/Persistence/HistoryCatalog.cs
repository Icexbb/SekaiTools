using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsInfrastructure.Persistence;

public sealed class HistorySummary
{
    public string VideoFilePath { get; init; } = "";
    public string ScriptFilePath { get; init; } = "";
    public string TranslateFilePath { get; init; } = "";
    public ProcessStopReason StopReason { get; init; }
}

public sealed record HistoryCatalogEntry(string FilePath, string Timestamp, HistorySummary Summary, bool IsHistory)
{
    public ProcessingState LoadState()
    {
        using var stream = File.OpenRead(FilePath);
        return (IsHistory
            ? JsonSerializer.Deserialize<HistoryEntry>(stream)?.State
            : JsonSerializer.Deserialize<ProcessingState>(stream))
            ?? throw new InvalidDataException("记录中缺少处理状态。");
    }
}

/// <summary>Lists metadata without allocating per-frame matcher state. Load the selected file on demand.</summary>
public static class HistoryCatalog
{
    private sealed class Envelope
    {
        public string Timestamp { get; init; } = "";
        public HistorySummary? State { get; init; }
    }

    public static IReadOnlyList<HistoryCatalogEntry> Read() => Read(ResourceManager.DataBaseDir);

    internal static IReadOnlyList<HistoryCatalogEntry> Read(string root)
    {
        var history = Scan(Path.Combine(root, "History"), true)
            .Where(entry => entry.Summary.StopReason == ProcessStopReason.Completed).ToList();
        var queueDirectory = Path.Combine(root, "SubtitleQueueProgress");
        var queue = Scan(queueDirectory, false)
            .Where(entry => entry.Summary.StopReason != ProcessStopReason.Completed).ToList();
        var legacy = Scan(Path.Combine(root, "Progress"), false)
            .Where(entry => entry.Summary.StopReason != ProcessStopReason.Completed)
            .OrderByDescending(entry => File.GetLastWriteTimeUtc(entry.FilePath)).Take(1)
            .Where(entry => !File.Exists(Path.Combine(queueDirectory, Path.GetFileName(entry.FilePath))));
        return queue.Concat(legacy).Concat(history).ToList();
    }

    private static IEnumerable<HistoryCatalogEntry> Scan(string directory, bool history)
    {
        if (!Directory.Exists(directory)) yield break;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(path => path))
        {
            HistoryCatalogEntry? entry = null;
            try
            {
                using var stream = File.OpenRead(path);
                if (history)
                {
                    var envelope = JsonSerializer.Deserialize<Envelope>(stream);
                    if (envelope?.State != null)
                        entry = new(path, envelope.Timestamp, envelope.State, true);
                }
                else if (JsonSerializer.Deserialize<HistorySummary>(stream) is { } summary)
                    entry = new(path, "未完成", summary, false);
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.Log($"历史摘要读取失败，已跳过: {path}, {exception.Message}", LogLevel.Warning);
            }
            if (entry != null) yield return entry;
        }
    }
}

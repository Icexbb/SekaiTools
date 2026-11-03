using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsInfrastructure.Persistence;

/// <summary>Queue tasks keep separate checkpoints so advancing cannot discard canceled results.</summary>
public static class SubtitleQueueProgressStore
{
    private static readonly string ProgressDirectory =
        Path.Combine(ResourceManager.DataBaseDir, "SubtitleQueueProgress");

    private static string GetPath(string key)
    {
        return Path.Combine(ProgressDirectory, $"{key}.json");
    }

    public static void Save(string key, ProcessingState state)
    {
        IncrementalProgressStore.Save(GetPath(key), state);
    }

    public static bool HasSavedState(string key)
    {
        return File.Exists(GetPath(key));
    }

    public static void Delete(string key)
    {
        var path = GetPath(key);
        IncrementalProgressStore.Delete(path);
    }

    public static IReadOnlyList<(string SaveKey, ProcessingState State)> EnumerateProgressFiles()
    {
        return EnumerateProgressFiles(ProgressDirectory);
    }

    internal static IReadOnlyList<(string SaveKey, ProcessingState State)> EnumerateProgressFiles(string directory)
    {
        var entries = new List<(string SaveKey, ProcessingState State)>();
        if (!Directory.Exists(directory)) return entries;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            try
            {
                var state = IncrementalProgressStore.Load(path);
                if (state != null && state.StopReason != ProcessStopReason.Completed)
                    entries.Add((Path.GetFileNameWithoutExtension(path), state));
            }
            catch (JsonException exception)
            {
                Logger.Log($"队列进度格式错误，已跳过: {path}, {exception.Message}", LogLevel.Warning);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException
                                                  or UnauthorizedAccessException)
            {
                Logger.Log($"队列进度读取失败，已跳过: {path}, {exception.Message}", LogLevel.Warning);
            }

        Logger.Log($"队列进度扫描完成: 待恢复任务={entries.Count}");
        return entries;
    }
}

public sealed class SubtitleQueueProcessingStatePersistence : IIncrementalCheckpointPersistence
{
    private SubtitleQueueProcessingStatePersistence()
    {
    }

    public static SubtitleQueueProcessingStatePersistence Instance { get; } = new();

    public void SaveProgress(string saveKey, ProcessingState state)
    {
        SubtitleQueueProgressStore.Save(saveKey, state);
    }

    public void DeleteProgress(string saveKey)
    {
        SubtitleQueueProgressStore.Delete(saveKey);
    }

    public void AddHistory(ProcessingState state)
    {
        HistoryStore.Add(state);
    }
}
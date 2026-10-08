using System.Text.Json;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsInfrastructure.Persistence;

/// <summary>Queue tasks keep separate checkpoints so advancing cannot discard canceled results.</summary>
public static class SubtitleQueueProgressStore
{
    private static readonly string ProgressDirectory = Path.Combine(ResourceManager.DataBaseDir, "SubtitleQueueProgress");
    private static string GetPath(string key) => Path.Combine(ProgressDirectory, $"{key}.json");

    public static void Save(string key, ProcessingState state) => ProgressStore.SaveToPath(GetPath(key), state);
    public static bool HasSavedState(string key) => File.Exists(GetPath(key));

    public static void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
    }

    public static IReadOnlyList<(string SaveKey, ProcessingState State)> EnumerateProgressFiles() =>
        EnumerateProgressFiles(ProgressDirectory);

    internal static IReadOnlyList<(string SaveKey, ProcessingState State)> EnumerateProgressFiles(string directory)
    {
        var entries = new List<(string SaveKey, ProcessingState State)>();
        if (!Directory.Exists(directory)) return entries;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var state = JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(path));
                if (state != null && state.StopReason != ProcessStopReason.Completed)
                    entries.Add((Path.GetFileNameWithoutExtension(path), state));
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return entries;
    }
}

public sealed class SubtitleQueueProcessingStatePersistence : IProcessingStatePersistence
{
    public static SubtitleQueueProcessingStatePersistence Instance { get; } = new();
    private SubtitleQueueProcessingStatePersistence() { }
    public void SaveProgress(string saveKey, ProcessingState state) => SubtitleQueueProgressStore.Save(saveKey, state);
    public void DeleteProgress(string saveKey) => SubtitleQueueProgressStore.Delete(saveKey);
    public void AddHistory(ProcessingState state) => HistoryStore.Add(state);
}

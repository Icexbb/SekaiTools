using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsInfrastructure.Persistence;

/// <summary>Consume last-session checkpoints before presenting the single recovery opportunity.</summary>
public static class SubtitleRecoveryStore
{
    public static IReadOnlyList<(string SaveKey, ProcessingState State)> TakeOnce() => TakeOnce(ResourceManager.DataBaseDir);

    internal static IReadOnlyList<(string SaveKey, ProcessingState State)> TakeOnce(string root)
    {
        var queueDirectory = Path.Combine(root, "SubtitleQueueProgress");
        var legacyDirectory = Path.Combine(root, "Progress");
        var queued = SubtitleQueueProgressStore.EnumerateProgressFiles(queueDirectory);
        var legacy = SubtitleQueueProgressStore.EnumerateProgressFiles(legacyDirectory);
        var states = queued.Concat(legacy.Take(1)).DistinctBy(entry => entry.SaveKey).ToArray();
        // Remove even older legacy records, otherwise the next scan would expose
        // the previous "latest" checkpoint as another recovery opportunity.
        foreach (var entry in queued) File.Delete(Path.Combine(queueDirectory, entry.SaveKey + ".json"));
        foreach (var entry in legacy) File.Delete(Path.Combine(legacyDirectory, entry.SaveKey + ".json"));
        return states;
    }
}

/// <summary>Recovered tasks may produce completed history, but cannot create another recovery checkpoint.</summary>
public sealed class RecoveredTaskPersistence(IProcessingStatePersistence inner) : IProcessingStatePersistence
{
    public void SaveProgress(string saveKey, ProcessingState state) { }
    public void DeleteProgress(string saveKey) => inner.DeleteProgress(saveKey);
    public void AddHistory(ProcessingState state) => inner.AddHistory(state);
}

using System.Text.Json;

namespace SekaiToolsCore.Process;

public class HistoryEntry
{
    public string Timestamp { get; init; } = "";
    public ProcessingState State { get; init; } = null!;
}

public static class HistoryStore
{
    private const int MaxEntries = 100;

    private static readonly string HistoryDir =
        Path.Combine(ResourceManager.DataBaseDir, "History");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Add(ProcessingState state)
    {
        var dir = HistoryDir;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var saveKey = ProgressStore.GetSaveKey(state.VideoFilePath, state.ScriptFilePath, state.TranslateFilePath);

        // 删除相同 hash 的旧记录，只保留最新一条
        foreach (var oldFile in Directory.EnumerateFiles(dir, $"*_{saveKey}.json"))
        {
            try { File.Delete(oldFile); }
            catch { /* ignore */ }
        }

        var ts = DateTime.Now;
        var timestamp = ts.ToString("yyyy-MM-dd HH:mm:ss");
        var fileName = $"{ts:yyyyMMdd_HHmmss}_{saveKey}.json";

        var entry = new HistoryEntry { Timestamp = timestamp, State = state };
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        File.WriteAllText(Path.Combine(dir, fileName), json);

        PruneIfNeeded();
    }

    public static List<HistoryEntry> LoadAll()
    {
        var dir = HistoryDir;
        if (!Directory.Exists(dir))
            return [];

        var result = new List<HistoryEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderByDescending(f => f))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<HistoryEntry>(json);
                if (entry != null)
                    result.Add(entry);
            }
            catch
            {
                // skip corrupt files
            }
        }

        return result;
    }

    private static void PruneIfNeeded()
    {
        var dir = HistoryDir;
        var files = Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f).ToList();
        if (files.Count <= MaxEntries) return;

        foreach (var file in files.Take(files.Count - MaxEntries))
        {
            try { File.Delete(file); }
            catch { /* ignore */ }
        }
    }
}

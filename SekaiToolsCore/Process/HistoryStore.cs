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

    private static readonly string HistoryPath =
        Path.Combine(ResourceManager.DataBaseDir, "History", "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Add(ProcessingState state)
    {
        var entries = LoadAll();
        entries.Insert(0, new HistoryEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            State = state
        });

        if (entries.Count > MaxEntries)
            entries = entries.Take(MaxEntries).ToList();

        var dir = Path.GetDirectoryName(HistoryPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(HistoryPath, json);
    }

    public static List<HistoryEntry> LoadAll()
    {
        if (!File.Exists(HistoryPath))
            return [];

        try
        {
            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

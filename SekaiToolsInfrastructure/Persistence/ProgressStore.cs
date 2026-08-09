using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsInfrastructure.Persistence;

public static class ProgressStore
{
    private static readonly string ProgressDir =
        Path.Combine(ResourceManager.DataBaseDir, "Progress");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetSaveKey(string videoPath, string scriptPath, string translatePath)
    {
        var joined = $"{videoPath}|{scriptPath}|{translatePath}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash, 0, 8);
    }

    public static string GetSavePath(string saveKey)
    {
        return Path.Combine(ProgressDir, $"{saveKey}.json");
    }

    public static bool HasSavedState(string saveKey)
    {
        return File.Exists(GetSavePath(saveKey));
    }

    public static void Save(string saveKey, ProcessingState state)
    {
        SaveToPath(GetSavePath(saveKey), state);
    }

    internal static void SaveToPath(string path, ProcessingState state)
    {
        var dir = Path.GetDirectoryName(path)
                  ?? throw new InvalidDataException($"进度保存路径无效: {path}");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static ProcessingState? Load(string saveKey)
    {
        var path = GetSavePath(saveKey);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string saveKey)
    {
        var path = GetSavePath(saveKey);
        if (File.Exists(path)) File.Delete(path);
    }

    public static List<(string SaveKey, ProcessingState State)> EnumerateProgressFiles()
    {
        var result = new List<(string, ProcessingState)>();
        if (!Directory.Exists(ProgressDir)) return result;

        foreach (var file in Directory.EnumerateFiles(ProgressDir, "*.json"))
        {
            var key = Path.GetFileNameWithoutExtension(file);
            var state = Load(key);
            if (state != null)
                result.Add((key, state));
        }

        return result;
    }
}

public sealed class ProcessingStatePersistence : IProcessingStatePersistence
{
    public static ProcessingStatePersistence Instance { get; } = new();

    private ProcessingStatePersistence()
    {
    }

    public void SaveProgress(string saveKey, ProcessingState state)
    {
        ProgressStore.Save(saveKey, state);
    }

    public void AddHistory(ProcessingState state)
    {
        HistoryStore.Add(state);
    }
}

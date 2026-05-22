using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SekaiToolsCore.Process;

// --- Point DTO ---
public record PointDto(int X, int Y);

// --- Frame Result DTOs ---
public record FrameResultDto(int Index, int X, int Y);

// --- FrameSet DTOs ---
public class DialogFrameSetDto
{
    public bool Finished { get; init; }
    public bool UseSeparator { get; init; }
    public int SeparateFrame { get; init; }
    public int SeparatorContentIndex { get; init; }
    public List<FrameResultDto> Frames { get; init; } = [];
}

public class BannerFrameSetDto
{
    public bool Finished { get; init; }
    public int Start { get; init; } = -1;
    public int End { get; init; } = -1;
}

public class MarkerFrameSetDto
{
    public bool Finished { get; init; }
    public List<FrameResultDto> Frames { get; init; } = [];
}

// --- Matcher State DTOs ---
public class DialogMatcherStateDto
{
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public PointDto? NameTagPosition { get; init; }
    public List<DialogFrameSetDto> FrameSets { get; init; } = [];
}

public class BannerMatcherStateDto
{
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public List<BannerFrameSetDto> FrameSets { get; init; } = [];
}

public class MarkerMatcherStateDto
{
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public List<MarkerFrameSetDto> FrameSets { get; init; } = [];
}

// --- Top-level Processing State ---
public class ProcessingState
{
    public string Version { get; init; } = "1.0";
    public int FrameIndex { get; init; }
    public bool ContentFinished { get; init; }
    public string VideoFilePath { get; init; } = "";
    public string ScriptFilePath { get; init; } = "";
    public string TranslateFilePath { get; init; } = "";
    public DialogMatcherStateDto? Dialog { get; init; }
    public BannerMatcherStateDto? Banner { get; init; }
    public MarkerMatcherStateDto? Marker { get; init; }
}

// --- File I/O Helper ---
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

    public static string GetSavePath(string saveKey) =>
        Path.Combine(ProgressDir, $"{saveKey}.json");

    public static bool HasSavedState(string saveKey) =>
        File.Exists(GetSavePath(saveKey));

    public static void Save(string saveKey, ProcessingState state)
    {
        var dir = ProgressDir;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var path = GetSavePath(saveKey);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static ProcessingState? Load(string saveKey)
    {
        var path = GetSavePath(saveKey);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProcessingState>(json);
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

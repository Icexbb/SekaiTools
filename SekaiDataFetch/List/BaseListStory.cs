using System.Reflection;
using System.Text.Json;
using SekaiDataFetch.Source;
using SekaiToolsBase;

namespace SekaiDataFetch.List;

[AttributeUsage(AttributeTargets.Property)]
public class CachePathAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}

[AttributeUsage(AttributeTargets.Property)]
public class SourcePathAttribute(string key, bool optional = false) : Attribute
{
    public string Key { get; } = key;
    public bool Optional { get; } = optional;
}

public abstract class BaseListStory
{
    protected static readonly Fetcher Fetcher = Fetcher.Instance;

    public static readonly string DataBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools");

    private string[] CachePaths
    {
        get
        {
            var properties = GetType().GetProperties(BindingFlags.NonPublic |
                                                     BindingFlags.Public |
                                                     BindingFlags.Static);
            return properties
                .Where(p => p.GetCustomAttributes(typeof(CachePathAttribute), false).Length != 0)
                .Select(p => p.GetValue(null) as string)
                .Where(s => s != null)
                .ToArray()!;
        }
    }

    public void SetSource(SourceData sourceData)
    {
        Fetcher.SetSource(sourceData);
    }

    public void SetProxy(Proxy proxy)
    {
        Fetcher.SetProxy(proxy);
    }

    public void ClearCache()
    {
        foreach (var path in CachePaths)
            if (File.Exists(path))
                File.Delete(path);

        Logger.Log($"{GetType().Name} cache cleared");
    }

    public static string GetCacheDirectory(SourceData source)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var name = new string(source.SourceName.Select(character =>
            char.IsControl(character) || invalidCharacters.Contains(character) ||
            "<>:\"/\\|?*".Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');
        if (name.Length == 0) name = "未命名数据源";

        // Windows reserves device names even when they have an extension.
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(COM|LPT)[1-9¹²³]$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            name = "_" + name;

        return Path.Combine(DataBaseDir, "Data", "cache", name);
    }

    protected static string CacheDirectory => GetCacheDirectory(Fetcher.SourceList.SourceData);

    public void ReloadFromCache() => Load();

    protected abstract void Load();

    public async Task Refresh(IProgress<ListRefreshProgress>? progress = null)
    {
        var type = GetType();

        var sourceProps = type.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(p => new
            {
                Prop = p,
                Attr = p.GetCustomAttributes(typeof(SourcePathAttribute), false).FirstOrDefault() as SourcePathAttribute
            })
            .Where(x => x.Attr is { Key.Length: > 0 })
            .ToDictionary(x => x.Attr?.Key!, x => x.Prop.GetValue(null) as string);

        var cacheFields = type.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(p => new
            {
                Prop = p,
                Attr = p.GetCustomAttributes(typeof(CachePathAttribute), false).FirstOrDefault() as CachePathAttribute
            })
            .Where(x => x.Attr is { Key.Length: > 0 })
            .ToDictionary(x => x.Attr?.Key!, x => x.Prop.GetValue(null) as string);

        var optionalKeys = type.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(p => p.GetCustomAttribute<SourcePathAttribute>())
            .Where(attribute => attribute is { Optional: true }).Select(attribute => attribute!.Key).ToHashSet();

        var keys = sourceProps.Keys.Intersect(cacheFields.Keys)
            .Where(key => sourceProps[key] != null && cacheFields[key] != null).ToArray();
        var progressLock = new object();
        var completed = new HashSet<string>();
        var unavailable = new HashSet<string>();
        void Report(ListRefreshStage stage, string? completedKey = null, bool failed = false)
        {
            if (progress == null) return;
            lock (progressLock)
            {
                if (completedKey != null)
                {
                    if (failed) unavailable.Add(completedKey);
                    else completed.Add(completedKey);
                }
                progress.Report(new ListRefreshProgress(stage, keys.Select(key =>
                    new ListRefreshFileProgress(Path.GetFileName(cacheFields[key]!), completed.Contains(key), unavailable.Contains(key)))
                    .ToArray()));
            }
        }

        Report(ListRefreshStage.Downloading);
        var tasks = keys
            .Select(async key =>
            {
                var sourceValue = sourceProps[key]!;
                var cachePath = cacheFields[key]!;
                try
                {
                    var content = await Fetcher.Fetch(sourceValue);
                    using var _ = JsonDocument.Parse(content);
                    Report(ListRefreshStage.Downloading, key);
                    return (CachePath: cachePath, Content: (string?)content);
                }
                catch (Exception exception) when (optionalKeys.Contains(key) &&
                    exception is HttpRequestException or TaskCanceledException or JsonException)
                {
                    Logger.Log($"{type.Name} optional data {key} unavailable: {exception.Message}",
                        Microsoft.Extensions.Logging.LogLevel.Warning);
                    Report(ListRefreshStage.Downloading, key, true);
                    return (CachePath: cachePath, Content: (string?)null);
                }
            }).ToArray();

        var downloads = await Task.WhenAll(tasks);
        Report(ListRefreshStage.Saving);
        foreach (var (cachePath, content) in downloads)
        {
            if (content == null)
            {
                // Do not reuse metadata from a previous source after this source failed.
                if (File.Exists(cachePath)) File.Delete(cachePath);
                continue;
            }
            var directory = Path.GetDirectoryName(cachePath)
                            ?? throw new InvalidDataException($"缓存路径无效: {cachePath}");
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory, $".{Path.GetFileName(cachePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(tempPath, content);
                File.Move(tempPath, cachePath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        Logger.Log($"{type.Name} data refreshed from sources: {string.Join(", ", sourceProps.Keys)}");

        Report(ListRefreshStage.Loading);
        Load();
        Report(ListRefreshStage.Completed);
    }
}

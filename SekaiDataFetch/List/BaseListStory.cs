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
public class SourcePathAttribute(string key) : Attribute
{
    public string Key { get; } = key;
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

    protected abstract void Load();

    public async Task Refresh()
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

        var tasks = sourceProps.Keys.Intersect(cacheFields.Keys)
            .Select(async key =>
            {
                var sourceValue = sourceProps[key];
                var cachePath = cacheFields[key];
                if (sourceValue != null && cachePath != null)
                {
                    var content = await Fetcher.Fetch(sourceValue);
                    using var _ = JsonDocument.Parse(content);
                    return (CachePath: cachePath, Content: content);
                }

                return (CachePath: (string?)null, Content: (string?)null);
            }).ToArray();

        var downloads = await Task.WhenAll(tasks);
        foreach (var (cachePath, content) in downloads)
        {
            if (cachePath == null || content == null) continue;
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

        Load();
    }
}

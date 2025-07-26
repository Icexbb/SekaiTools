using System.Reflection;
using Microsoft.Extensions.Logging;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.List;

[AttributeUsage(AttributeTargets.Property)]
public class CachePathAttribute(string key) : Attribute
{
    public string Key { get; } = key;
};

[AttributeUsage(AttributeTargets.Property)]
public class SourcePathAttribute(string key) : Attribute
{
    public string Key { get; } = key;
};

public abstract class BaseListStory
{
    protected static readonly Fetcher Fetcher = Fetcher.Instance;

    protected string[] CachePaths
    {
        get
        {
            var fields = GetType().GetFields(System.Reflection.BindingFlags.NonPublic |
                                             System.Reflection.BindingFlags.Static |
                                             System.Reflection.BindingFlags.Instance);
            return fields
                .Where(f => f.GetCustomAttributes(typeof(CachePathAttribute), false).Length != 0)
                .Select(f => f.GetValue(this) as string)
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
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Log.Logger.LogInformation("{TypeName} cache cleared", GetType().Name);
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
                    await File.WriteAllTextAsync(cachePath, content);
                }
            }).ToArray();

        await Task.WhenAll(tasks);

        Log.Logger.LogInformation("{TypeName} data refreshed from sources: {Sources}", type.Name,
            string.Join(", ", sourceProps.Keys));

        Load();
    }
}
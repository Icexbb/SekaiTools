using Microsoft.Extensions.Logging;
using SekaiDataFetch.Item;
using SekaiToolsBase;
using SekaiToolsBase.Data;

namespace SekaiDataFetch.List;

public class ListUnitStory : BaseListStory
{
    public readonly Dictionary<string, UnitStorySet> Data = new();

    private ListUnitStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    [CachePath("unitStories")]
    private static string CachePathUnitStories =>
        Path.Combine(DataBaseDir, "Data", "cache", "unitStories.json");

    [SourcePath("unitStories")] private static string SourceUnitStories => Fetcher.SourceList.UnitStories;

    public static ListUnitStory Instance { get; } = new();


    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathUnitStories)!);
        if (!File.Exists(CachePathUnitStories)) return;

        var json = File.ReadAllText(CachePathUnitStories);

        try
        {
            var unitStories = Utils.Deserialize<UnitStory[]>(json);
            if (unitStories == null) throw new Exception("Json parse error");
            GetData(unitStories);
        }
        catch (Exception e)
        {
            Logger.Log(
                $"{GetType().Name} Failed to load data. Clearing cache and retrying. Error: {e.Message}", LogLevel.Error
            );
            ClearCache();
        }
    }

    private void GetData(UnitStory[] unitStory)
    {
        foreach (var unit in unitStory)
        {
            var data = new UnitStorySet(unit);
            Data.Set(unit.Unit, data);
        }
    }
}
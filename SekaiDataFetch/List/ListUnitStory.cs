using Microsoft.Extensions.Logging;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListUnitStory : BaseListStory
{
    [CachePath("unitStories")]
    private static string CachePathUnitStories =>
        Path.Combine(DataBaseDir, "Data", "cache", "unitStories.json");

    [SourcePath("unitStories")] private static string SourceUnitStories => Fetcher.SourceList.UnitStories;

    public readonly Dictionary<string, UnitStorySet> Data = new();

    private ListUnitStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

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
            Log.Logger.LogError(e,
                "{TypeName} Failed to load data. Clearing cache and retrying. Error: {Message}",
                GetType().Name, e.Message);
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
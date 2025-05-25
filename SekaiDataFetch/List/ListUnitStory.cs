using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListUnitStory : BaseListStory
{
    private static readonly string CachePathUnitStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "unitStories.json");

    public readonly Dictionary<string, UnitStorySet> Data = new();

    private ListUnitStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListUnitStory Instance { get; } = new();


    public async Task Refresh()
    {
        var stringUnitStories = await Fetcher.Fetch(Fetcher.SourceList.UnitStories);
        await File.WriteAllTextAsync(CachePathUnitStories, stringUnitStories);
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathUnitStories)!);
        if (!File.Exists(CachePathUnitStories)) return;

        var json = File.ReadAllText(CachePathUnitStories);
        var unitStories = Utils.Deserialize<UnitStory[]>(json);
        if (unitStories == null) throw new Exception("Json parse error");
        GetData(unitStories);
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
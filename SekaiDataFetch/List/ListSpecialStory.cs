using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListSpecialStory : BaseListStory
{
    private static readonly string CachePathSpecialStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "specialStories.json");

    public readonly Dictionary<string, SpecialStorySet> Data = new();

    private ListSpecialStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListSpecialStory Instance { get; } = new();


    public async Task Refresh()
    {
        var stringSpecialStories = await Fetcher.Fetch(Fetcher.SourceList.SpecialStories);
        await File.WriteAllTextAsync(CachePathSpecialStories, stringSpecialStories);
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathSpecialStories)!);
        if (!File.Exists(CachePathSpecialStories)) return;

        var stringSpecialStories = File.ReadAllText(CachePathSpecialStories);

        var specialStories = Utils.Deserialize<SpecialStory[]>(stringSpecialStories);
        if (specialStories == null) throw new Exception("Json parse error");
        GetData(specialStories);
    }

    private void GetData(ICollection<SpecialStory> specialStory)
    {
        foreach (var special in specialStory)
        {
            var data = new SpecialStorySet(special);
            Data.Set(special.Title, data);
        }
    }
}
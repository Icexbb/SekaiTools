using Microsoft.Extensions.Logging;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListSpecialStory : BaseListStory
{
    [CachePath("specialStories")]
    private static string CachePathSpecialStories =>
        Path.Combine(DataBaseDir, "Data", "cache", "specialStories.json");

    [SourcePath("specialStories")] private static string SourceSpecialStories => Fetcher.SourceList.SpecialStories;


    public readonly Dictionary<string, SpecialStorySet> Data = new();

    private ListSpecialStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListSpecialStory Instance { get; } = new();


    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathSpecialStories)!);
        if (!File.Exists(CachePathSpecialStories)) return;

        var stringSpecialStories = File.ReadAllText(CachePathSpecialStories);

        try
        {
            var specialStories = Utils.Deserialize<SpecialStory[]>(stringSpecialStories);
            if (specialStories == null) throw new Exception("Json parse error");
            GetData(specialStories);
        }
        catch (Exception e)
        {
            Log.Logger.LogError(e,
                "{TypeName} Failed to load data. Clearing cache and retrying. Error: {Message}",
                GetType().Name, e.Message);
            ClearCache();
        }
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
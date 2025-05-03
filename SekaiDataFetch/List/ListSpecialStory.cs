using SekaiDataFetch.Data;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.List;

public class ListSpecialStory : BaseListStory
{
    private static readonly string CachePathSpecialStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "specialStories.json");

    public readonly Dictionary<string, SpecialStorySet> Data = new();

    public ListSpecialStory(SourceType sourceType = SourceType.SiteBest, Proxy? proxy = null)
    {
        SetSource(sourceType);
        SetProxy(proxy ?? Proxy.None);
        Load();
    }


    public async Task Refresh()
    {
        var stringSpecialStories = await Fetcher.Fetch(Fetcher.Source.SpecialStories);
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
            var episodeDict = new List<SpecialStorySet.Episode>();
            episodeDict.AddRange(special.Episodes.Select(episode =>
                new SpecialStorySet.Episode(episode.Title, episode.AssetBundleName, episode.ScenarioId)));

            var data = new SpecialStorySet(special.Title, episodeDict.ToArray());
            Data.Set(special.Title, data);
        }
    }
}
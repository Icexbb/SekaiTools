using SekaiDataFetch.Data;

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

public class SpecialStorySet(string title, SpecialStorySet.Episode[]? episodes = null)
{
    public string Title { get; } = title;
    public Episode[] Episodes { get; } = episodes ?? [];

    public class Episode(string title, string assetBundleName, string scenarioId)
    {
        public string Title { get; } = title;
        public string ScenarioId { get; } = scenarioId;
        public string AssetBundleName { get; } = assetBundleName;

        public string Url(SourceType sourceType = 0)
        {
            return sourceType switch
            {
                SourceType.SiteBest =>
                    $"https://storage.sekai.best/sekai-jp-assets/scenario/special/" +
                    $"{AssetBundleName}_rip/{ScenarioId}.asset",
                SourceType.SiteAi =>
                    $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/special/" +
                    $"{AssetBundleName}/{ScenarioId}.json",
                SourceType.SiteHaruki =>
                    $"https://storage.haruki.wacca.cn/assets/startapp/scenario/special/" +
                    $"{AssetBundleName}/{ScenarioId}.json",
                _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
            };
        }
    }
}
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListSpecialStory
{
    private static readonly string CachePathSpecialStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "specialStories.json");

    public readonly Dictionary<string, SpecialStorySet> Data = new();

    public ListSpecialStory(SourceList.SourceType sourceType = SourceList.SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
        Load();
    }

    private Fetcher Fetcher { get; }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathSpecialStories)!);

        if (!File.Exists(CachePathSpecialStories)) return;
        var data = File.ReadAllText(CachePathSpecialStories);
        var jObj = JsonConvert.DeserializeObject<JObject[]>(data);
        if (jObj != null) GetData(jObj.Select(SpecialStory.FromJson).ToList());
    }

    public async Task Refresh()
    {
        var json = await Fetcher.FetchSource(Fetcher.Source.SpecialStories);
        if (json == null) throw new Exception("Failed to fetch special stories");
        await File.WriteAllTextAsync(CachePathSpecialStories, JsonConvert.SerializeObject(json));
        GetData(json.Select(SpecialStory.FromJson).ToList());
    }

    private void GetData(IEnumerable<SpecialStory> specialStory)
    {
        foreach (var special in specialStory)
        {
            var episodeDict = new List<SpecialStorySet.Episode>();
            episodeDict.AddRange(special.Episodes.Select(episode =>
                new SpecialStorySet.Episode(episode.Title, episode.AssetBundleName, episode.ScenarioId)));

            var data = new SpecialStorySet(special.Title, episodeDict.ToArray());
            Data.Add(special.Title, data);
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

            public string Url(SourceList.SourceType sourceType = 0)
            {
                return sourceType switch
                {
                    SourceList.SourceType.SiteBest =>
                        $"https://storage.sekai.best/sekai-jp-assets/scenario/special" +
                        $"/{AssetBundleName}_rip/{ScenarioId}.asset",
                    SourceList.SourceType.SiteAi =>
                        $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/special" +
                        $"/{AssetBundleName}/{ScenarioId}.json",
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                };
            }
        }
    }
}
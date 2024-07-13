using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListUnitStory
{
    private Fetcher Fetcher { get; }

    private static readonly string CachePathUnitStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "unitStories.json");

    public ListUnitStory(SourceList.SourceType sourceType = SourceList.SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathUnitStories)!);

        if (!File.Exists(CachePathUnitStories)) return;
        var data = File.ReadAllText(CachePathUnitStories);
        var jObj = JsonConvert.DeserializeObject<JObject[]>(data);
        if (jObj != null) GetData(jObj.Select(UnitStory.FromJson).ToList());
    }

    public async Task Refresh()
    {
        var json = await Fetcher.FetchSource(Fetcher.Source.UnitStories);
        if (json == null) throw new Exception("Failed to fetch unit stories");
        await File.WriteAllTextAsync(CachePathUnitStories, JsonConvert.SerializeObject(json));
        GetData(json.Select(UnitStory.FromJson).ToList());
    }

    private void GetData(IEnumerable<UnitStory> unitStory)
    {
        foreach (var unit in unitStory)
        {
            var chapterDict = new List<UnitStorySet.Chapter>();
            foreach (var chapter in unit.Chapters)
            {
                var episodeDict = new List<UnitStorySet.Chapter.Episode>();
                episodeDict.AddRange(chapter.Episodes.Select(episode =>
                    new UnitStorySet.Chapter.Episode(episode.EpisodeNoLabel, episode.Title, episode.ScenarioId)));

                chapterDict.Add(new UnitStorySet.Chapter(chapter.Title, chapter.AssetBundleName,
                    episodeDict.ToArray()));
            }

            var data = new UnitStorySet(Constants.UnitName[unit.Unit], chapterDict.ToArray());
            Data.Add(unit.Unit, data);
        }
    }

    public readonly Dictionary<string, UnitStorySet> Data = new();

    public class UnitStorySet(string name, UnitStorySet.Chapter[]? chapters = null)
    {
        public class Chapter(string name = "", string assetBundleName = "", Chapter.Episode[]? episodes = null)
        {
            public class Episode(string episodeNoLabel, string title, string scenarioId)
            {
                private string EpisodeNoLabel { get; init; } = episodeNoLabel;
                private string Title { get; init; } = title;
                private string ScenarioId { get; init; } = scenarioId;

                public string Key => $"{EpisodeNoLabel} - {Title}";

                public string Url(string assetBundleName, SourceList.SourceType sourceType = 0) => sourceType switch
                {
                    SourceList.SourceType.SiteBest =>
                        $"https://storage.sekai.best/sekai-jp-assets/scenario/unitstory" +
                        $"/{assetBundleName}_rip/{ScenarioId}.asset",
                    SourceList.SourceType.SiteAi =>
                        $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/unitstory" +
                        $"/{assetBundleName}/{ScenarioId}.json",
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                };
            }

            public string Name { get; init; } = name;

            public string AssetBundleName { get; init; } = assetBundleName;

            public Episode[] Episodes { get; init; } = episodes ?? [];
        }

        public string Name { get; init; } = name;

        public Chapter[] Chapters { get; init; } = chapters ?? [];
    }
}
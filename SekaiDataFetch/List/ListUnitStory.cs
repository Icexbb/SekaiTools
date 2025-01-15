using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListUnitStory : BaseListStory
{
    private static readonly string CachePathUnitStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "unitStories.json");

    public readonly Dictionary<string, UnitStorySet> Data = new();

    public ListUnitStory(SourceType sourceType = SourceType.SiteBest, Proxy? proxy = null)
    {
        SetSource(sourceType);
        SetProxy(proxy ?? Proxy.None);
        Load();
    }


    public async Task Refresh()
    {
        var stringUnitStories = await Fetcher.Fetch(Fetcher.Source.UnitStories);
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
            Data.Set(unit.Unit, data);
        }
    }

    public class UnitStorySet(string name, UnitStorySet.Chapter[]? chapters = null)
    {
        public string Name { get; init; } = name;

        public Chapter[] Chapters { get; init; } = chapters ?? [];

        public class Chapter(string name = "", string assetBundleName = "", Chapter.Episode[]? episodes = null)
        {
            public string Name { get; init; } = name;

            public string AssetBundleName { get; init; } = assetBundleName;

            public Episode[] Episodes { get; init; } = episodes ?? [];

            public class Episode(string episodeNoLabel, string title, string scenarioId)
            {
                private string EpisodeNoLabel { get; } = episodeNoLabel;
                private string Title { get; } = title;
                private string ScenarioId { get; } = scenarioId;

                public string Key => $"{EpisodeNoLabel} - {Title}";

                public string Url(string assetBundleName, SourceType sourceType = 0)
                {
                    return sourceType switch
                    {
                        SourceType.SiteBest =>
                            $"https://storage.sekai.best/sekai-jp-assets/scenario/unitstory/" +
                            $"{assetBundleName}_rip/{ScenarioId}.asset",
                        SourceType.SiteAi =>
                            $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/unitstory/" +
                            $"{assetBundleName}/{ScenarioId}.json",
                        SourceType.SiteHaruki =>
                            $"https://storage.haruki.wacca.cn/assets/startapp/scenario/unitstory/" +
                            $"{assetBundleName}/{ScenarioId}.json",
                        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                    };
                }
            }
        }
    }
}
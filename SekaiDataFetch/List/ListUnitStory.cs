using SekaiDataFetch.Data;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

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
}
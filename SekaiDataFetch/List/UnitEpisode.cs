using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class UnitEpisode
{
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> Data = new();


    public UnitEpisode(IEnumerable<UnitStory> unitStory,
        SourceList.SourceType sourceType = SourceList.SourceType.SiteBest)
    {
        foreach (var unit in unitStory)
        {
            var chapterDict = new Dictionary<string, Dictionary<string, string>>();
            foreach (var chapter in unit.Chapters)
            {
                var episodeDict = new Dictionary<string, string>();
                foreach (var episode in chapter.Episodes)
                {
                    var key = $"{episode.EpisodeNoLabel} - {episode.Title}";
                    var url = sourceType switch
                    {
                        SourceList.SourceType.SiteBest =>
                            $"https://storage.sekai.best/sekai-jp-assets/scenario/unitstory" +
                            $"/{chapter.AssetBundleName}_rip/{episode.ScenarioId}.asset",
                        SourceList.SourceType.SiteAi =>
                            $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/unitstory" +
                            $"/{chapter.AssetBundleName}/{episode.ScenarioId}.json",
                        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                    };
                    episodeDict.Add(key, url);
                }

                chapterDict.Add(chapter.Title, episodeDict);
            }

            Data.Add(Constants.UnitName[unit.Unit], chapterDict);
        }
    }
}
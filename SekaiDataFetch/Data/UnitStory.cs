using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class UnitEpisode
{
    public int Id { get; set; }
    public int UnitStoryEpisodeGroupId { get; set; }
    public int ChapterNo { get; set; }
    public int EpisodeNo { get; set; }
    public string EpisodeNoLabel { get; set; } = "";
    public string Title { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public int[] RewardResourceBoxIds { get; set; } = [];

    public static UnitEpisode FromJson(JObject json)
    {
        return new UnitEpisode
        {
            Id = json.Get("id", 0),
            UnitStoryEpisodeGroupId = json.Get("unitStoryEpisodeGroupId", 0),
            ChapterNo = json.Get("chapterNo", 0),
            EpisodeNo = json.Get("episodeNo", 0),
            EpisodeNoLabel = json.Get("episodeNoLabel", ""),
            Title = json.Get("title", ""),
            AssetbundleName = json.Get("assetbundleName", ""),
            ScenarioId = json.Get("scenarioId", ""),
            ReleaseConditionId = json.Get("releaseConditionId", 0),
            RewardResourceBoxIds = json.Get("rewardResourceBoxIds", Array.Empty<int>())
        };
    }
}

public struct UnitChapter
{
    public int Id { set; get; }
    public string Unit { set; get; }
    public int ChapterNo { set; get; }
    public string Title { set; get; }
    public string AssetBundleName { set; get; }
    public UnitEpisode[] Episodes { set; get; }

    public static UnitChapter FromJson(JObject json)
    {
        return new UnitChapter
        {
            Id = json.Get("id", 0),
            Unit = json.Get("unit", ""),
            ChapterNo = json.Get("chapterNo", 0),
            Title = json.Get("title", ""),
            AssetBundleName = json.Get("assetbundleName", ""),
            Episodes = json.Get("episodes", Array.Empty<JObject>()).Select(UnitEpisode.FromJson).ToArray()
        };
    }
}

public struct UnitStory
{
    public string Unit { get; set; }
    public int Seq { get; set; }
    public UnitChapter[] Chapters { get; set; }

    public static UnitStory FromJson(JObject json)
    {
        return new UnitStory
        {
            Unit = json.Get("unit", ""),
            Seq = json.Get("seq", 0),
            Chapters = json.Get("chapters", Array.Empty<JObject>()).Select(UnitChapter.FromJson).ToArray()
        };
    }
}
using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class SpecialStoryEpisode
{
    public int Id { get; set; }
    public int SpecialStoryId { get; set; }
    public int EpisodeNo { get; set; }
    public string Title { get; set; } = "";
    public string SpecialStoryEpisodeType { get; set; } = "";
    public string AssetBundleName { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public bool IsAbleSkip { get; set; }
    public bool IsActionSetRefresh { get; set; }
    public int[] RewardResourceBoxIds { get; set; } = [];

    public static SpecialStoryEpisode FromJson(JObject json)
    {
        return new SpecialStoryEpisode
        {
            Id = json.Get("id", 0),
            SpecialStoryId = json.Get("specialStoryId", 0),
            EpisodeNo = json.Get("episodeNo", 0),
            Title = json.Get("title", ""),
            SpecialStoryEpisodeType = json.Get("specialStoryEpisodeType", ""),
            AssetBundleName = json.Get("assetbundleName", ""),
            ScenarioId = json.Get("scenarioId", ""),
            ReleaseConditionId = json.Get("releaseConditionId", 0),
            IsAbleSkip = json.Get("isAbleSkip", false),
            IsActionSetRefresh = json.Get("isActionSetRefresh", false),
            RewardResourceBoxIds = json.Get("rewardResourceBoxIds", Array.Empty<int>())
        };
    }
}

public struct SpecialStory
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Title { get; set; }
    public string AssetBundleName { get; set; }
    public long StartAt { get; set; }
    public long EndAt { get; set; }
    public SpecialStoryEpisode[] Episodes { get; set; }

    public static SpecialStory FromJson(JObject json)
    {
        return new SpecialStory
        {
            Id = json.Get("id", 0),
            Seq = json.Get("seq", 0),
            Title = json.Get("title", ""),
            AssetBundleName = json.Get("assetbundleName", ""),
            StartAt = json.Get("startAt", 0L),
            EndAt = json.Get("endAt", 0L),
            Episodes = json.Get("episodes", Array.Empty<JObject>()).Select(SpecialStoryEpisode.FromJson).ToArray()
        };
    }
}
using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Fetch.Data;

public class SpecialStoryEpisode
{
    public int Id { get; set; }
    public int SpecialStoryId { get; set; }
    public int EpisodeNo { get; set; }
    public string Title { get; set; }
    public string SpecialStoryEpisodeType { get; set; }
    public string AssetBundleName { get; set; }
    public string ScenarioId { get; set; }
    public int ReleaseConditionId { get; set; }
    public bool IsAbleSkip { get; set; }
    public bool IsActionSetRefresh { get; set; }
    public int[] RewardResourceBoxIds { get; set; }

    public static SpecialStoryEpisode FromJson(JObject json)
    {
        return new SpecialStoryEpisode
        {
            Id = json["id"]!.ToObject<int>(),
            SpecialStoryId = json["specialStoryId"]!.ToObject<int>(),
            EpisodeNo = json["episodeNo"]!.ToObject<int>(),
            Title = json["title"]!.ToObject<string>()!,
            SpecialStoryEpisodeType = json["specialStoryEpisodeType"]!.ToObject<string>()!,
            AssetBundleName = json["assetbundleName"]!.ToObject<string>()!,
            ScenarioId = json["scenarioId"]!.ToObject<string>()!,
            ReleaseConditionId = json["releaseConditionId"]!.ToObject<int>(),
            IsAbleSkip = json["isAbleSkip"]!.ToObject<bool>(),
            IsActionSetRefresh = json["isActionSetRefresh"]!.ToObject<bool>(),
            RewardResourceBoxIds = json["rewardResourceBoxIds"]!.ToObject<int[]>() ?? Array.Empty<int>()
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
        return new SpecialStory()
        {
            Id = json["id"]!.ToObject<int>(),
            Seq = json["seq"]!.ToObject<int>(),
            Title = json["title"]!.ToObject<string>()!,
            AssetBundleName = json["assetbundleName"]!.ToObject<string>()!,
            StartAt = json["startAt"]!.ToObject<long>(),
            EndAt = json["endAt"]!.ToObject<long>(),
            Episodes = json["episodes"]!.ToObject<JObject[]>()!.Select(SpecialStoryEpisode.FromJson).ToArray()
        };
    }
}
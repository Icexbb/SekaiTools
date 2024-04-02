using Newtonsoft.Json.Linq;

namespace SekaiToolsCore.Story.Fetch.Data;

public class UnitEpisode
{
    public int Id { get; set; }
    public int UnitStoryEpisodeGroupId { get; set; }
    public int ChapterNo { get; set; }
    public int EpisodeNo { get; set; }
    public string EpisodeNoLabel { get; set; }
    public string Title { get; set; }
    public string AssetbundleName { get; set; }
    public string ScenarioId { get; set; }
    public int ReleaseConditionId { get; set; }
    public int[] RewardResourceBoxIds { get; set; }

    public static UnitEpisode FromJson(JObject json)
    {
        return new UnitEpisode
        {
            Id = json["id"]!.ToObject<int>(),
            UnitStoryEpisodeGroupId = json["unitStoryEpisodeGroupId"]!.ToObject<int>(),
            ChapterNo = json["chapterNo"]!.ToObject<int>(),
            EpisodeNo = json["episodeNo"]!.ToObject<int>(),
            EpisodeNoLabel = json["episodeNoLabel"]!.ToObject<string>()!,
            Title = json["title"]!.ToObject<string>()!,
            AssetbundleName = json["assetbundleName"]!.ToObject<string>()!,
            ScenarioId = json["scenarioId"]!.ToObject<string>()!,
            ReleaseConditionId = json["releaseConditionId"]!.ToObject<int>(),
            RewardResourceBoxIds = json["rewardResourceBoxIds"]!.ToObject<int[]>() ?? Array.Empty<int>()
        };
    }
}

public struct UnitChapter
{
    public int Id { set; get; }
    public string Unit { set; get; }
    public int ChapterNo { set; get; }
    public string Title { set; get; }
    public string AssetbundleName { set; get; }
    public UnitEpisode[] Episodes { set; get; }

    public static UnitChapter FromJson(JObject json)
    {
        return new UnitChapter
        {
            Id = json["id"]!.ToObject<int>(),
            Unit = json["unit"]!.ToObject<string>()!,
            ChapterNo = json["chapterNo"]!.ToObject<int>(),
            Title = json["title"]!.ToObject<string>()!,
            AssetbundleName = json["assetbundleName"]!.ToObject<string>()!,
            Episodes = json["episodes"]!.ToObject<JObject[]>()!.Select(UnitEpisode.FromJson).ToArray()
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
            Unit = json["unit"]!.ToObject<string>()!,
            Seq = json["seq"]!.ToObject<int>(),
            Chapters = json["chapters"]!.ToObject<JObject[]>()!.Select(UnitChapter.FromJson).ToArray()
        };
    }
}
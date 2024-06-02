using Newtonsoft.Json.Linq;
namespace SekaiDataFetch.Data;

public class EpisodeReward
{
    public string StoryType { get; set; }
    public int ResourceBoxId { get; set; }

    public static EpisodeReward FromJson(JObject json)
    {
        return new EpisodeReward
        {
            StoryType = json["storyType"]!.ToObject<string>()!,
            ResourceBoxId = json["resourceBoxId"]!.ToObject<int>()
        };
    }
}

public class EventEpisode
{
    public int Id { get; set; }
    public int EventStoryId { get; set; }
    public int EpisodeNo { get; set; }
    public string Title { get; set; }
    public string AssetbundleName { get; set; }
    public string ScenarioId { get; set; }
    public int ReleaseConditionId { get; set; }
    public EpisodeReward[] EpisodeRewards { get; set; }

    public static EventEpisode FromJson(JObject json)
    {
        return new EventEpisode
        {
            Id = json["id"]!.ToObject<int>(),
            EventStoryId = json["eventStoryId"]!.ToObject<int>(),
            EpisodeNo = json["episodeNo"]!.ToObject<int>(),
            Title = json["title"]!.ToObject<string>()!,
            AssetbundleName = json["assetbundleName"]!.ToObject<string>()!,
            ScenarioId = json["scenarioId"]!.ToObject<string>()!,
            ReleaseConditionId = json["releaseConditionId"]!.ToObject<int>(),
            EpisodeRewards = json["episodeRewards"]!.ToObject<JObject[]>()!.Select(EpisodeReward.FromJson).ToArray()
        };
    }
}

public class EventStory
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Outline { get; set; }
    public int BannerGameCharacterUnitId { get; set; }
    public string AssetbundleName { get; set; }
    public EventEpisode[] EventStoryEpisodes { get; set; }

    public static EventStory FromJson(JObject json)
    {
        return new EventStory
        {
            Id = json["id"]!.ToObject<int>(),
            EventId = json["eventId"]!.ToObject<int>(),
            Outline = json["outline"]!.ToObject<string>()!,
            BannerGameCharacterUnitId = json["bannerGameCharacterUnitId"]!.ToObject<int>(),
            AssetbundleName = json["assetbundleName"]!.ToObject<string>()!,
            EventStoryEpisodes = json["eventStoryEpisodes"]!.ToObject<JObject[]>()!.Select(EventEpisode.FromJson)
                .ToArray()
        };
    }
}
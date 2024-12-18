using Newtonsoft.Json.Linq;

namespace SekaiDataFetch.Data;

public class EpisodeReward : ICloneable
{
    public string StoryType { get; set; } = "";
    public int ResourceBoxId { get; set; }

    public object Clone()
    {
        return new EpisodeReward
        {
            StoryType = StoryType,
            ResourceBoxId = ResourceBoxId
        };
    }

    public static EpisodeReward FromJson(JObject json)
    {
        return new EpisodeReward
        {
            StoryType = json.Get("storyType", ""),
            ResourceBoxId = json.Get("resourceBoxId", 0)
        };
    }
}

public class EventEpisode : ICloneable
{
    public int Id { get; set; }
    public int EventStoryId { get; set; }
    public int EpisodeNo { get; set; }
    public string Title { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int ReleaseConditionId { get; set; }
    public EpisodeReward[] EpisodeRewards { get; set; } = [];

    public object Clone()
    {
        return new EventEpisode
        {
            Id = Id,
            EventStoryId = EventStoryId,
            EpisodeNo = EpisodeNo,
            Title = Title,
            AssetbundleName = AssetbundleName,
            ScenarioId = ScenarioId,
            ReleaseConditionId = ReleaseConditionId,
            EpisodeRewards = EpisodeRewards.Select(x => (EpisodeReward)x.Clone()).ToArray()
        };
    }

    public static EventEpisode FromJson(JObject json)
    {
        return new EventEpisode
        {
            Id = json.Get("id", 0),
            EventStoryId = json.Get("eventStoryId", 0),
            EpisodeNo = json.Get("episodeNo", 0),
            Title = json.Get("title", ""),
            AssetbundleName = json.Get("assetbundleName", ""),
            ScenarioId = json.Get("scenarioId", ""),
            ReleaseConditionId = json.Get("releaseConditionId", 0),
            EpisodeRewards = json.Get("episodeRewards", Array.Empty<JObject>()).Select(EpisodeReward.FromJson).ToArray()
        };
    }
}

public class EventStory : ICloneable
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Outline { get; set; } = "";
    public int BannerGameCharacterUnitId { get; set; }
    public string AssetbundleName { get; set; } = "";
    public EventEpisode[] EventStoryEpisodes { get; set; } = [];

    public object Clone()
    {
        return new EventStory
        {
            Id = Id,
            EventId = EventId,
            Outline = Outline,
            BannerGameCharacterUnitId = BannerGameCharacterUnitId,
            AssetbundleName = AssetbundleName,
            EventStoryEpisodes = EventStoryEpisodes.Select(x => (EventEpisode)x.Clone()).ToArray()
        };
    }

    public static EventStory FromJson(JObject json)
    {
        return new EventStory
        {
            Id = json.Get("id", 0),
            EventId = json.Get("eventId", 0),
            Outline = json.Get("outline", ""),
            BannerGameCharacterUnitId = json.Get("bannerGameCharacterUnitId", 0),
            AssetbundleName = json.Get("assetbundleName", ""),
            EventStoryEpisodes = json.Get("eventStoryEpisodes", Array.Empty<JObject>()).Select(EventEpisode.FromJson)
                .ToArray()
        };
    }
}
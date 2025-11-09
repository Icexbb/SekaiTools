namespace SekaiToolsBase.DataList;

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
}

public class EventStory : ICloneable
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Outline { get; set; } = "";
    public int BannerGameCharacterUnitId { get; set; }
    public string AssetBundleName { get; set; } = "";
    public EventEpisode[] EventStoryEpisodes { get; set; } = [];

    public object Clone()
    {
        return new EventStory
        {
            Id = Id,
            EventId = EventId,
            Outline = Outline,
            BannerGameCharacterUnitId = BannerGameCharacterUnitId,
            AssetBundleName = AssetBundleName,
            EventStoryEpisodes = EventStoryEpisodes.Select(x => (EventEpisode)x.Clone()).ToArray()
        };
    }
}
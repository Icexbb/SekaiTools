using SekaiDataFetch.Data;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.Item;

public class EventStoryImpl(EventStory es, GameEvent ge) : ICloneable
{
    public readonly EventStory EventStory = es;
    public readonly GameEvent GameEvent = ge;

    public object Clone()
    {
        return new EventStoryImpl((EventStory)EventStory.Clone(), (GameEvent)GameEvent.Clone());
    }

    public string Url(int episode, SourceType sourceType)
    {
        if (episode < 0 || episode >= EventStory.EventStoryEpisodes.Length)
            throw new ArgumentOutOfRangeException(nameof(episode), episode, null);
        var abName = EventStory.AssetBundleName;
        var scenarioId = EventStory.EventStoryEpisodes[episode].ScenarioId;
        return sourceType switch
        {
            SourceType.SiteBest =>
                $"https://storage.sekai.best/sekai-jp-assets/event_story/" +
                $"{abName}/scenario_rip/{scenarioId}.asset",
            SourceType.SiteHaruki =>
                "https://storage.haruki.wacca.cn/assets/ondemand/event_story/" +
                $"{abName}/scenario/{scenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}
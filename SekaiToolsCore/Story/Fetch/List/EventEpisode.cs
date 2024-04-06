namespace SekaiToolsCore.Story.Fetch.List;

public class EventEpisode()
{
    public Dictionary<string, Dictionary<string, string>> Data = new();

    public EventEpisode(IEnumerable<Data.EventStory> evStories, ICollection<Data.GameEvent> events,
        Fetcher.SourceList.SourceType sourceType = Fetcher.SourceList.SourceType.SekaiBest) : this()
    {
        foreach (var eventStory in evStories)
        {
            var @event = events.FirstOrDefault(x => x.Id == eventStory.EventId);
            var eventName = $"{eventStory.EventId:000}: {(@event == null ? @event.Name : $"未知活动")}";
            var eventEp = new Dictionary<string, string>();
            foreach (var episode in eventStory.EventStoryEpisodes)
            {
                eventEp.Set($"{episode.EpisodeNo}: {episode.Title}",
                    sourceType == Fetcher.SourceList.SourceType.SekaiBest
                        ? $"https://storage.sekai.best/sekai-assets/event_story/{eventStory.AssetbundleName}/scenario_rip/{episode.ScenarioId}.asset"
                        : $"https://assets.pjsek.ai/file/pjsekai-assets/ondemand/event_story/{eventStory.AssetbundleName}/scenario/{episode.ScenarioId}.json");
            }

            Data.Set(eventName, eventEp);
        }
    }
}
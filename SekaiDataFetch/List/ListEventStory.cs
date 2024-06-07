namespace SekaiDataFetch.List;

public class ListEventStory()
{
    public Dictionary<string, Dictionary<string, string>> Data = new();

    public ListEventStory(IEnumerable<Data.EventStory> evStories, ICollection<Data.GameEvent> events,
        SourceList.SourceType sourceType = SourceList.SourceType.SiteBest) : this()
    {
        foreach (var eventStory in evStories)
        {
            var @event = events.FirstOrDefault(x => x.Id == eventStory.EventId);
            var eventEp = eventStory.EventStoryEpisodes.ToDictionary(episode => $"{episode.EpisodeNo}: {episode.Title}",
                episode => sourceType switch
                {
                    SourceList.SourceType.SiteBest => $"https://storage.sekai.best/sekai-assets/event_story" +
                                                      $"/{eventStory.AssetbundleName}/scenario_rip" +
                                                      $"/{episode.ScenarioId}.asset",
                    SourceList.SourceType.SiteAi => $"https://assets.pjsek.ai/file/pjsekai-assets/ondemand" +
                                                    $"/event_story/{eventStory.AssetbundleName}/scenario" +
                                                    $"/{episode.ScenarioId}.json",
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
                });
            Data.Add($"{eventStory.EventId:000}: {(@event == null ? @event?.Name : $"未知活动")}", eventEp);
        }
    }
}
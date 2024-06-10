namespace SekaiDataFetch.List;

public class ListEventStory
{
    private Fetcher? Fetcher { get; set; } = null;

    public ListEventStory(SourceList.SourceType sourceType = SourceList.SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
    }

    public async Task Refresh()
    {
        if (Fetcher == null) throw new NullReferenceException();
        var eventStories = await Fetcher!.GetEventStories();
        var gameEvents = await Fetcher!.GetGameEvents();
        GetData(eventStories, gameEvents);
    }

    public ListEventStory(ICollection<Data.EventStory> evStories, ICollection<Data.GameEvent> events)
    {
        GetData(evStories, events);
    }

    private void GetData(ICollection<Data.EventStory> evStories, ICollection<Data.GameEvent> events)
    {
        if (evStories.Count != events.Count)
            throw new ArgumentException("EventStory and GameEvent count mismatch", nameof(evStories));
        foreach (var eventStory in evStories)
        {
            var @event = events.FirstOrDefault(x => x.Id == eventStory.EventId);
            if (@event == null)
                throw new ArgumentException("EventStory and GameEvent mismatch", nameof(evStories));
            Data.Add(new EventStoryImpl(eventStory, @event));
        }
    }

    public readonly List<EventStoryImpl> Data = [];

    public class EventStoryImpl(Data.EventStory es, Data.GameEvent ge)
    {
        public readonly Data.EventStory EventStory = es;
        public readonly Data.GameEvent GameEvent = ge;

        public string Url(int episode, SourceList.SourceType sourceType)
        {
            if (episode < 0 || episode >= EventStory.EventStoryEpisodes.Length)
                throw new ArgumentOutOfRangeException(nameof(episode), episode, null);
            string abName = EventStory.AssetbundleName;
            return sourceType switch
            {
                SourceList.SourceType.SiteBest => $"https://storage.sekai.best/sekai-assets/event_story" +
                                                  $"/{abName}/scenario_rip" +
                                                  $"/{EventStory.EventStoryEpisodes[episode].ScenarioId}.asset",
                SourceList.SourceType.SiteAi => $"https://assets.pjsek.ai/file/pjsekai-assets/ondemand" +
                                                $"/event_story/{abName}/scenario" +
                                                $"/{EventStory.EventStoryEpisodes[episode].ScenarioId}.json",
                _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
            };
        }
    }
}
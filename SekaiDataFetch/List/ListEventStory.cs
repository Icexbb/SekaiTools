using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListEventStory
{
    private Fetcher Fetcher { get; }


    private static readonly string CachePathEventStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "eventStories.json");

    private static readonly string CachePathGameEvents =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "gameEvents.json");

    public ListEventStory(SourceList.SourceType sourceType = SourceList.SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathEventStories)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathGameEvents)!);

        if (!File.Exists(CachePathEventStories) || !File.Exists(CachePathGameEvents)) return;
        var dataEventStories = File.ReadAllText(CachePathEventStories);
        var dataGameEvents = File.ReadAllText(CachePathGameEvents);
        var jObjEventStories = JsonConvert.DeserializeObject<JObject[]>(dataEventStories);
        var jObjGameEvents = JsonConvert.DeserializeObject<JObject[]>(dataGameEvents);
        if (jObjEventStories != null && jObjGameEvents != null)
            GetData(jObjEventStories.Select(EventStory.FromJson).ToList(),
                jObjGameEvents.Select(GameEvent.FromJson).ToList());
    }

    public async Task Refresh()
    {
        var jsonEventStories = await Fetcher.FetchSource(Fetcher.Source.EventStories);
        var jsonGameEvents = await Fetcher.FetchSource(Fetcher.Source.Events);
        if (jsonEventStories == null || jsonGameEvents == null)
            throw new Exception("Failed to fetch event stories or game events");
        await File.WriteAllTextAsync(CachePathEventStories, JsonConvert.SerializeObject(jsonEventStories));
        await File.WriteAllTextAsync(CachePathGameEvents, JsonConvert.SerializeObject(jsonGameEvents));
        GetData(jsonEventStories.Select(EventStory.FromJson).ToList(),
            jsonGameEvents.Select(GameEvent.FromJson).ToList());
    }


    private void GetData(ICollection<EventStory> evStories, ICollection<GameEvent> events)
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

        // Sort by EventId Biggest to Smallest
        Data.Sort((x, y) => y.EventStory.EventId.CompareTo(x.EventStory.EventId));
    }

    public readonly List<EventStoryImpl> Data = [];

    public class EventStoryImpl(EventStory es, GameEvent ge)
    {
        public readonly EventStory EventStory = es;
        public readonly GameEvent GameEvent = ge;

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
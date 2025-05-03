using SekaiDataFetch.Data;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiDataFetch.List;

public class ListEventStory : BaseListStory
{
    private static readonly string CachePathEventStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "eventStories.json");

    private static readonly string CachePathGameEvents =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "gameEvents.json");

    public readonly List<EventStoryImpl> Data = [];

    public ListEventStory(SourceType sourceType = SourceType.SiteBest, Proxy? proxy = null)
    {
        SetSource(sourceType);
        SetProxy(proxy ?? Proxy.None);
        Load();
    }


    public async Task Refresh()
    {
        var stringEventStories = await Fetcher.Fetch(Fetcher.Source.EventStories);
        var stringGameEvents = await Fetcher.Fetch(Fetcher.Source.Events);
        await File.WriteAllTextAsync(CachePathEventStories, stringEventStories);
        await File.WriteAllTextAsync(CachePathGameEvents, stringGameEvents);
        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathEventStories)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathGameEvents)!);
        if (!File.Exists(CachePathEventStories) || !File.Exists(CachePathGameEvents)) return;

        var stringEventStories = File.ReadAllText(CachePathEventStories);
        var stringGameEvents = File.ReadAllText(CachePathGameEvents);

        var eventStories = Utils.Deserialize<EventStory[]>(stringEventStories);
        var gameEvents = Utils.Deserialize<GameEvent[]>(stringGameEvents);
        if (eventStories == null || gameEvents == null) throw new Exception("Json parse error");
        GetData(eventStories, gameEvents);
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
}
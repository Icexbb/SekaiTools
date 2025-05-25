using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListEventStory : BaseListStory
{
    private static readonly string CachePathEventStories =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "eventStories.json");

    private static readonly string CachePathGameEvents =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "gameEvents.json");

    public readonly List<EventStorySet> Data = [];

    private ListEventStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListEventStory Instance { get; } = new();


    public async Task Refresh()
    {
        var stringEventStories = await Fetcher.Fetch(Fetcher.SourceList.EventStories);
        var stringGameEvents = await Fetcher.Fetch(Fetcher.SourceList.Events);
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
        // evStories may not be the same as events
        // if (evStories.Count != events.Count)
        // throw new ArgumentException("EventStory and GameEvent count mismatch", nameof(evStories));
        foreach (var eventStory in evStories)
        {
            var @event = events.FirstOrDefault(x => x.Id == eventStory.EventId);
            if (@event == null)
                throw new ArgumentException("EventStory and GameEvent mismatch", nameof(evStories));
            Data.Add(new EventStorySet(eventStory, @event));
        }

        // Sort by EventId Biggest to Smallest
        Data.Sort((x, y) => y.EventStory.EventId.CompareTo(x.EventStory.EventId));
    }
}
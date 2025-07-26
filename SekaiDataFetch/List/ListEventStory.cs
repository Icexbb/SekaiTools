using Microsoft.Extensions.Logging;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListEventStory : BaseListStory
{
    [CachePath("eventStories")]
    private static string CachePathEventStories =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "eventStories.json");

    [CachePath("gameEvents")]
    private static string CachePathGameEvents =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "gameEvents.json");

    [SourcePath("eventStories")] private static string SourceEventStories => Fetcher.SourceList.EventStories;
    [SourcePath("gameEvents")] private static string SourceGameEvents => Fetcher.SourceList.Events;

    public readonly List<EventStorySet> Data = [];

    private ListEventStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public static ListEventStory Instance { get; } = new();


    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathEventStories)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathGameEvents)!);
        if (!File.Exists(CachePathEventStories) || !File.Exists(CachePathGameEvents)) return;

        var stringEventStories = File.ReadAllText(CachePathEventStories);
        var stringGameEvents = File.ReadAllText(CachePathGameEvents);

        try
        {
            var eventStories = Utils.Deserialize<EventStory[]>(stringEventStories);
            var gameEvents = Utils.Deserialize<GameEvent[]>(stringGameEvents);
            if (eventStories == null || gameEvents == null) throw new Exception("Json parse error");
            GetData(eventStories, gameEvents);
        }
        catch (Exception e)
        {
            Log.Logger.LogError(e,
                "{TypeName} Failed to load data. Clearing cache and retrying. Error: {Message}",
                GetType().Name, e.Message);
            ClearCache();
        }
    }

    private void GetData(ICollection<EventStory> evStories, ICollection<GameEvent> events)
    {
        // evStories may not be the same as events
        // if (evStories.Count != events.Count)
        // throw new ArgumentException("EventStory and GameEvent count mismatch", nameof(evStories));

        var stories = evStories.ToList();
        stories.Sort((x, y) => x.Id.CompareTo(y.Id));
        for (var i = 0; i < stories.Count; i++)
        {
            var story = stories[i];
            var @event = events.FirstOrDefault(x => x.Id == story.EventId);
            if (@event == null)
                throw new ArgumentException("EventStory and GameEvent mismatch", nameof(evStories));
            Data.Add(new EventStorySet(story, @event, i + 1));
        }
    }
}
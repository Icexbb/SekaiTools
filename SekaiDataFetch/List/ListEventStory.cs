using Microsoft.Extensions.Logging;
using SekaiDataFetch.Item;
using SekaiToolsBase;
using SekaiToolsBase.DataList;

namespace SekaiDataFetch.List;

public class ListEventStory : BaseListStory
{
    public readonly List<EventStorySet> Data = [];

    private ListEventStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    [CachePath("eventStories")]
    private static string CachePathEventStories =>
        Path.Combine(DataBaseDir, "Data", "cache", "eventStories.json");

    [CachePath("gameEvents")]
    private static string CachePathGameEvents =>
        Path.Combine(DataBaseDir, "Data", "cache", "gameEvents.json");

    [CachePath("worldBlooms")]
    private static string CachePathWorldBlooms =>
        Path.Combine(DataBaseDir, "Data", "cache", "worldBlooms.json");

    [SourcePath("eventStories")] private static string SourceEventStories => Fetcher.SourceList.EventStories;
    [SourcePath("gameEvents")] private static string SourceGameEvents => Fetcher.SourceList.Events;
    [SourcePath("worldBlooms")] private static string SourceWorldBlooms => Fetcher.SourceList.WorldBlooms;

    public static ListEventStory Instance { get; } = new();


    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathEventStories)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathGameEvents)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathWorldBlooms)!);
        if (!File.Exists(CachePathEventStories) || !File.Exists(CachePathGameEvents) ||
            !File.Exists(CachePathWorldBlooms)) return;

        var stringEventStories = File.ReadAllText(CachePathEventStories);
        var stringGameEvents = File.ReadAllText(CachePathGameEvents);
        var stringWorldBlooms = File.ReadAllText(CachePathWorldBlooms);

        try
        {
            var eventStories = Utils.Deserialize<EventStory[]>(stringEventStories);
            var gameEvents = Utils.Deserialize<GameEvent[]>(stringGameEvents);
            var worldBlooms = Utils.Deserialize<WorldBloomChapter[]>(stringWorldBlooms);
            if (eventStories == null || gameEvents == null || worldBlooms == null)
                throw new Exception("Json parse error");
            GetData(eventStories, gameEvents, worldBlooms);
        }
        catch (Exception e)
        {
            Logger.Log(
                $"{GetType().Name} Failed to load data. Clearing cache and retrying. Error: {e.Message}",
                LogLevel.Error);
            ClearCache();
        }
    }

    private void GetData(ICollection<EventStory> evStories, ICollection<GameEvent> events,
        ICollection<WorldBloomChapter> worldBlooms)
    {
        // evStories may not be the same as events
        // if (evStories.Count != events.Count)
        // throw new ArgumentException("EventStory and GameEvent count mismatch", nameof(evStories));

        Data.Clear();
        var stories = evStories.ToList();
        stories.Sort((x, y) => x.Id.CompareTo(y.Id));
        for (var i = 0; i < stories.Count; i++)
        {
            var story = stories[i];
            var @event = events.FirstOrDefault(x => x.Id == story.EventId);
            if (@event == null)
                throw new ArgumentException("EventStory and GameEvent mismatch", nameof(evStories));
            var bannerGameCharacterIds = @event.EventType == "world_bloom"
                ? WorldBloomChapter.GetBannerGameCharacterIds(worldBlooms, story.EventId)
                : [];
            Data.Add(new EventStorySet(story, @event, i + 1, bannerGameCharacterIds));
        }
    }
}

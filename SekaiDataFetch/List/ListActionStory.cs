using Microsoft.Extensions.Logging;
using SekaiDataFetch.Item;
using SekaiToolsBase;
using SekaiToolsBase.DataList;

namespace SekaiDataFetch.List;

public class ListActionStory : BaseListStory
{
    private ListActionStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    [CachePath("areas")]
    private static string CachePathAreas =>
        Path.Combine(DataBaseDir, "Data", "cache", "areas.json");

    [CachePath("actionSets")]
    private static string CachePathActionSets =>
        Path.Combine(DataBaseDir, "Data", "cache", "actionSets.json");

    [CachePath("character2ds")]
    private static string CachePathCharacter2ds =>
        Path.Combine(DataBaseDir, "Data", "cache", "character2ds.json");

    [SourcePath("areas")] private static string SourceAreas => Fetcher.SourceList.Areas;
    [SourcePath("actionSets")] private static string SourceActionSets => Fetcher.SourceList.ActionSets;
    [SourcePath("character2ds")] private static string SourceCharacter2ds => Fetcher.SourceList.Character2ds;

    [CachePath("releaseConditions")]
    private static string CachePathReleaseConditions => Path.Combine(DataBaseDir, "Data", "cache", "releaseConditions.json");
    [CachePath("eventStories")]
    private static string CachePathEventStories => Path.Combine(DataBaseDir, "Data", "cache", "eventStories.json");
    [CachePath("gameEvents")]
    private static string CachePathGameEvents => Path.Combine(DataBaseDir, "Data", "cache", "gameEvents.json");
    [SourcePath("releaseConditions", optional: true)] private static string SourceReleaseConditions => Fetcher.SourceList.ReleaseConditions;
    [SourcePath("eventStories", optional: true)] private static string SourceEventStories => Fetcher.SourceList.EventStories;
    [SourcePath("gameEvents", optional: true)] private static string SourceGameEvents => Fetcher.SourceList.Events;

    public bool ActivityMetadataAvailable { get; private set; }

    public List<AreaStorySet> Data { get; set; } = [];
    public List<Area> Areas { get; private set; } = [];

    public List<Character2d> Character2ds { get; private set; } = [];
    public static ListActionStory Instance { get; } = new();


    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathActionSets)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathAreas)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathCharacter2ds)!);
        if (!File.Exists(CachePathActionSets) ||
            !File.Exists(CachePathAreas) ||
            !File.Exists(CachePathCharacter2ds)) return;

        var stringActionSets = File.ReadAllText(CachePathActionSets);
        var stringAreas = File.ReadAllText(CachePathAreas);
        var stringCharacter2ds = File.ReadAllText(CachePathCharacter2ds);

        try
        {
            var areas = Utils.Deserialize<Area[]>(stringAreas);
            var actionSets = Utils.Deserialize<ActionSet[]>(stringActionSets);
            var character2ds = Utils.Deserialize<Character2d[]>(stringCharacter2ds);

            if (actionSets == null || areas == null || character2ds == null) throw new Exception("Json parse error");
            GetData(actionSets, areas, character2ds);
            LoadActivityMetadata(actionSets);
        }
        catch (Exception e)
        {
            Logger.Log(
                $"{GetType().Name} Failed to load data. Clearing cache and retrying. Error: {e.Message}",
                LogLevel.Error);
            ClearCache();
        }
    }

    private void GetData(ActionSet[] actionSets, Area[] areas, Character2d[] character2ds)
    {
        var stories = new List<AreaStorySet>();
        foreach (var actionSet in actionSets)
        {
            var area = areas.FirstOrDefault(area => area.Id == actionSet.AreaId);
            if (area == null) continue;
            if (actionSet.ScenarioId == "") continue;
            var data = new AreaStorySet(actionSet)
            {
                AreaName = area.AreaName,
                CharacterIds = actionSet.CharacterIds
                    .Select(id => character2ds.First(c2d => c2d.Id == id).CharacterId)
                    .ToArray()
            };

            stories.Add(data);
        }

        Data.Clear();
        Data.AddRange(stories);
        Areas = areas.Select(area => (Area)area.Clone()).ToList();
        Character2ds = character2ds.Select(character2d => (Character2d)character2d.Clone()).ToList();
    }

    private void LoadActivityMetadata(ActionSet[] actionSets)
    {
        ActivityMetadataAvailable = false;
        // Classification works even without any activity data.
        ActionStoryMetadata.Apply(Data, actionSets, [], [], []);
        if (!File.Exists(CachePathReleaseConditions) || !File.Exists(CachePathEventStories) || !File.Exists(CachePathGameEvents)) return;
        try
        {
            var conditions = Utils.Deserialize<ReleaseCondition[]>(File.ReadAllText(CachePathReleaseConditions));
            var stories = Utils.Deserialize<EventStory[]>(File.ReadAllText(CachePathEventStories));
            var events = Utils.Deserialize<GameEvent[]>(File.ReadAllText(CachePathGameEvents));
            if (conditions == null || stories == null || events == null) throw new InvalidDataException("活动筛选数据无效");
            ActionStoryMetadata.Apply(Data, actionSets, conditions, stories, events);
            ActivityMetadataAvailable = conditions.Length > 0 && stories.Any(story => story.EventStoryEpisodes.Length > 0) && events.Length > 0;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or IOException or ArgumentException)
        {
            ActionStoryMetadata.Apply(Data, actionSets, [], [], []);
            Logger.Log($"活动筛选数据不可用，保留地图对话基础筛选：{exception.Message}", LogLevel.Warning);
        }
    }
}

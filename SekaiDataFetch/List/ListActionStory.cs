using Microsoft.Extensions.Logging;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListActionStory : BaseListStory
{
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

    private ListActionStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

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
        }
        catch (Exception e)
        {
            Log.Logger.LogError(e,
                "{TypeName} Failed to load data. Clearing cache and retrying. Error: {Message}",
                GetType().Name, e.Message);
            ClearCache();
        }
    }

    private void GetData(ActionSet[] actionSets, Area[] areas, Character2d[] character2ds)
    {
        foreach (var actionSet in actionSets)
        {
            var area = areas.FirstOrDefault(area => area.Id == actionSet.AreaId);
            if (area == null) continue;
            if (actionSet.ScenarioId == "") continue;
            var data = new AreaStorySet(actionSet)
            {
                CharacterIds = actionSet.CharacterIds
                    .Select(id => character2ds.First(c2d => c2d.Id == id).CharacterId)
                    .ToArray()
            };

            Data.Add(data);
        }

        Areas = areas.Select(area => (Area)area.Clone()).ToList();
        Character2ds = character2ds.Select(character2d => (Character2d)character2d.Clone()).ToList();
    }
}
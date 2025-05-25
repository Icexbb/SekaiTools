using SekaiDataFetch.Data;
using SekaiDataFetch.Item;

namespace SekaiDataFetch.List;

public class ListActionStory : BaseListStory
{
    private static readonly string CachePathAreas =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "areas.json");

    private static readonly string CachePathActionSets =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "actionSets.json");

    private static readonly string CachePathCharacter2ds =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "cache", "character2ds.json");

    private ListActionStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    public List<AreaStorySet> Data { get; set; } = [];
    public List<Area> Areas { get; private set; } = [];

    public List<Character2d> Character2ds { get; private set; } = [];
    public static ListActionStory Instance { get; } = new();


    public async Task Refresh()
    {
        var stringActionSets = await Fetcher.Fetch(Fetcher.SourceList.ActionSets);
        var stringAreas = await Fetcher.Fetch(Fetcher.SourceList.Areas);
        var stringCharacter2ds = await Fetcher.Fetch(Fetcher.SourceList.Character2ds);
        await File.WriteAllTextAsync(CachePathActionSets, stringActionSets);
        await File.WriteAllTextAsync(CachePathAreas, stringAreas);
        await File.WriteAllTextAsync(CachePathCharacter2ds, stringCharacter2ds);

        Load();
    }

    private void Load()
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

        var areas = Utils.Deserialize<Area[]>(stringAreas);
        var actionSets = Utils.Deserialize<ActionSet[]>(stringActionSets);
        var character2ds = Utils.Deserialize<Character2d[]>(stringCharacter2ds);

        if (actionSets == null || areas == null || character2ds == null) throw new Exception("Json parse error");
        GetData(actionSets, areas, character2ds);
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
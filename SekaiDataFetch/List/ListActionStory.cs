using SekaiDataFetch.Data;

namespace SekaiDataFetch.List;

public class ListActionStory
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

    private Fetcher Fetcher { get; }
    public List<AreaStory> Data { get; set; } = [];
    public List<Area> Areas { get; private set; } = [];

    public List<Character2d> Character2ds { get; private set; } = [];

    public ListActionStory(SourceType sourceType = SourceType.SiteBest, Proxy? proxy = null)
    {
        var fetcher = new Fetcher();
        fetcher.SetSource(sourceType);
        fetcher.SetProxy(proxy ?? Proxy.None);
        Fetcher = fetcher;
        Load();
    }

    public async Task Refresh()
    {
        var stringActionSets = await Fetcher.Fetch(Fetcher.Source.ActionSets);
        var stringAreas = await Fetcher.Fetch(Fetcher.Source.Areas);
        var stringCharacter2ds = await Fetcher.Fetch(Fetcher.Source.Character2ds);
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
            var data = new AreaStory(actionSet)
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

public class AreaStory(ActionSet actionSet) : ICloneable
{
    public ActionSet ActionSet { get; } = actionSet;
    public string ScenarioId { get; } = actionSet.ScenarioId;
    public int Group { get; } = actionSet.Id / 100;

    public int[] CharacterIds { get; set; } = [];


    public object Clone()
    {
        return new AreaStory(ActionSet)
        {
            CharacterIds = CharacterIds
        };
    }

    public string Url(SourceType sourceType = SourceType.SiteBest)
    {
        return sourceType switch
        {
            SourceType.SiteBest =>
                $"https://storage.sekai.best/sekai-jp-assets/scenario/actionset" +
                $"/group{Group}_rip/{ScenarioId}.asset",
            SourceType.SiteAi =>
                $"https://assets.pjsek.ai/file/pjsekai-assets/startapp/scenario/actionset" +
                $"/group{Group}/{ScenarioId}.json",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };
    }
}
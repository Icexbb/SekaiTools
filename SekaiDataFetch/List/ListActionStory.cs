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

    private Fetcher Fetcher { get; }
    public readonly List<AreaStory> Data = [];

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
        await File.WriteAllTextAsync(CachePathActionSets, stringActionSets);
        await File.WriteAllTextAsync(CachePathAreas, stringAreas);

        Load();
    }

    private void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathActionSets)!);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathAreas)!);
        if (!File.Exists(CachePathActionSets) || !File.Exists(CachePathAreas)) return;

        var stringActionSets = File.ReadAllText(CachePathActionSets);
        var stringAreas = File.ReadAllText(CachePathAreas);

        var areas = Utils.Deserialize<Area[]>(stringAreas);
        var actionSets = Utils.Deserialize<ActionSet[]>(stringActionSets);

        if (actionSets == null || areas == null) throw new Exception("Json parse error");
        GetData(actionSets, areas);
    }

    private void GetData(ActionSet[] actionSets, Area[] areas)
    {
        foreach (var actionSet in actionSets)
        {
            var area = areas.FirstOrDefault(area => area.Id == actionSet.AreaId);
            if (area == null) continue;
            if (actionSet.ScenarioId == "") continue;
            var data = new AreaStory(actionSet);

            Data.Add(data);
        }
    }
}

public class AreaStory(ActionSet actionSet)
{
    public string ScenarioId { get; } = actionSet.ScenarioId;
    public int Group { get; } = actionSet.Id / 100;

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
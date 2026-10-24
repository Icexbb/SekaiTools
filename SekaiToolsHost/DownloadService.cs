using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsBase.GameScript;

namespace SekaiToolsHost;

public sealed record CatalogArgs(int Source, string Category, bool Refresh = false);
public sealed record DownloadArgs(int Source, string Category, string Id, string Output);
public sealed record DownloadRow(string Id, string Title, string Group, string Url);

public static class DownloadService
{
    public static async Task<DownloadRow[]> Catalog(CatalogArgs args)
    {
        var sources = SourceData.Default;
        if (args.Source < 0 || args.Source >= sources.Length) throw new InvalidDataException("数据源无效");
        Fetcher.Instance.SetSource(sources[args.Source]);
        var source = Fetcher.Instance.SourceList;
        BaseListStory list = args.Category switch
        {
            "event" => ListEventStory.Instance,
            "card" => ListCardStory.Instance,
            "unit" => ListUnitStory.Instance,
            "special" => ListSpecialStory.Instance,
            "action" => ListActionStory.Instance,
            _ => throw new InvalidDataException("未知剧本分类")
        };
        if (args.Refresh) await list.Refresh();
        else list.ReloadFromCache();
        var rows = args.Category switch
        {
            "event" => ListEventStory.Instance.Data.SelectMany(set => set.EventStory.EventStoryEpisodes.Select(ep =>
                new DownloadRow(ep.ScenarioId, ep.Title, set.GameEvent.Name,
                    source.EventStory(ep.ScenarioId, set.EventStory.AssetBundleName)))).ToArray(),
            "card" => ListCardStory.Instance.Data.SelectMany(set => new[] { set.FirstPart, set.SecondPart }.Select(ep =>
                new DownloadRow(ep.ScenarioId, ep.Title, set.Card.Prefix, source.MemberStory(ep)))).ToArray(),
            "unit" => ListUnitStory.Instance.Data.Values.SelectMany(set => set.Chapters.SelectMany(ch => ch.Episodes.Select(ep =>
                new DownloadRow(ep.ScenarioId, ep.Title, set.Name + " / " + ch.Name,
                    source.UnitStory(ep.ScenarioId, ch.AssetBundleName))))).ToArray(),
            "special" => ListSpecialStory.Instance.Data.Values.SelectMany(set => set.Episodes.Select(ep =>
                new DownloadRow(ep.ScenarioId, ep.Title, set.AssetBundleName, source.SpecialStory(ep)))).ToArray(),
            "action" => ListActionStory.Instance.Data.Select(set => new DownloadRow(set.ScenarioId,
                set.ScenarioId, set.AreaName + " / " + set.TypeName, source.ActionSet(set))).ToArray(),
            _ => []
        };
        return rows.Where(row => !string.IsNullOrWhiteSpace(row.Id)).DistinctBy(row => row.Id).ToArray();
    }

    public static async Task Save(DownloadArgs args)
    {
        // Resolve the URL from configured sources, rather than accepting arbitrary URLs from the UI.
        var rows = await Catalog(new CatalogArgs(args.Source, args.Category));
        var row = rows.FirstOrDefault(row => row.Id == args.Id) ?? throw new InvalidDataException("目录中没有此剧本，请刷新目录");
        var content = await Fetcher.Instance.Fetch(row.Url);
        var temp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(temp, content);
            _ = new GameScript(temp);
            Protocol.AtomicWrite(args.Output, content);
        }
        finally { File.Delete(temp); }
    }
}

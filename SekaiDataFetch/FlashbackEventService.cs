using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsBase;
using SekaiToolsBase.DataList;
using SekaiToolsBase.Story;

namespace SekaiDataFetch;

/// <summary>优先使用各源缓存，仅在无法解析时依次刷新主数据，不改变全局下载源。</summary>
public sealed class FlashbackEventService(
    Func<string, CancellationToken, Task<string>>? fetch = null,
    Func<SourceData, string>? cacheDirectory = null)
{
    private readonly Func<string, CancellationToken, Task<string>> _fetch = fetch ?? Fetcher.Instance.FetchOnceAsync;
    private readonly Func<SourceData, string> _cacheDirectory = cacheDirectory ?? BaseListStory.GetCacheDirectory;
    private static readonly (string File, string Type)[] Files =
        [("gameEvents", "events"), ("eventStories", "eventStories"), ("actionSets", "actionSets"), ("releaseConditions", "releaseConditions")];

    public async Task<IReadOnlyDictionary<string, FlashbackEventOrigin>> ResolveAsync(IEnumerable<string> sources,
        IEnumerable<SourceData> downloadSources, CancellationToken cancellationToken = default,
        Func<CancellationToken, Task<SourceData[]>>? loadAdditionalSources = null)
    {
        var unresolved = sources.Where(source => FlashbackEventCatalog.GetEventKey(source) != null)
            .ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, FlashbackEventOrigin>(StringComparer.Ordinal);
        if (unresolved.Count == 0) return result;
        var snapshots = new List<(SourceData Source, Dictionary<string, string> Content)>();
        foreach (var source in downloadSources.DistinctBy(source => (source.SourceName, source.SourceTemplate)))
        {
            await ReadCache(source).ConfigureAwait(false);
            if (unresolved.Count == 0) return result;
        }
        if (loadAdditionalSources != null)
        {
            try
            {
                var additional = await loadAdditionalSources(cancellationToken).ConfigureAwait(false);
                foreach (var source in additional.Where(source => source != null).DistinctBy(source => (source.SourceName, source.SourceTemplate)))
                {
                    if (snapshots.Any(snapshot => snapshot.Source.SourceTemplate == source.SourceTemplate &&
                        snapshot.Source.SourceName == source.SourceName)) continue;
                    await ReadCache(source).ConfigureAwait(false);
                    if (unresolved.Count == 0) return result;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is
                HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
            {
                Logger.Log($"闪回活动下载源列表获取失败，使用已知下载源: {exception.Message}", LogLevel.Warning);
            }
        }
        foreach (var (source, content) in snapshots.DistinctBy(snapshot => snapshot.Source.SourceTemplate))
        {
            foreach (var (file, type) in Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var json = await _fetch(source.SourceTemplate.Replace("{type}", type), cancellationToken).ConfigureAwait(false);
                    Validate(file, json);
                    content[file] = json;
                    var directory = _cacheDirectory(source);
                    Directory.CreateDirectory(directory);
                    var path = Path.Combine(directory, file + ".json");
                    var temporary = path + $".{Guid.NewGuid():N}.tmp";
                    try
                    {
                        await File.WriteAllTextAsync(temporary, json, cancellationToken).ConfigureAwait(false);
                        File.Move(temporary, path, true);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is
                    HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    Logger.Log($"闪回活动信息获取失败: {source.SourceName}/{file}: {exception.Message}", LogLevel.Warning);
                }
            }
            Resolve(content);
            if (unresolved.Count == 0) break;
        }
        return result;

        async Task ReadCache(SourceData source)
        {
            var content = new Dictionary<string, string>();
            foreach (var (file, _) in Files)
            {
                var path = Path.Combine(_cacheDirectory(source), file + ".json");
                try
                {
                    if (File.Exists(path))
                    {
                        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                        Validate(file, json);
                        content[file] = json;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    Logger.Log($"闪回活动缓存不可用: {path}: {exception.Message}", LogLevel.Warning);
                }
            }
            snapshots.Add((source, content));
            Resolve(content);
        }

        void Resolve(Dictionary<string, string> content)
        {
            var catalog = new FlashbackEventCatalog(Read<GameEvent>("gameEvents"), Read<EventStory>("eventStories"),
                Read<ActionSet>("actionSets"), Read<ReleaseCondition>("releaseConditions"));
            foreach (var source in unresolved.ToArray())
                if (catalog.Resolve(source) is { } origin)
                {
                    result[source] = origin;
                    unresolved.Remove(source);
                }
            T[] Read<T>(string file) => content.TryGetValue(file, out var json) ? Utils.Deserialize<T[]>(json)! : [];
        }
    }

    private static void Validate(string file, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.EnumerateArray()
            .Any(item => item.ValueKind != JsonValueKind.Object)) throw new InvalidDataException("主数据必须是对象数组");
        // 验证类型后才替换已有缓存，避免下载成功但字段无效的数据破坏可用元数据。
        var invalid = file switch
        {
            "gameEvents" => Utils.Deserialize<GameEvent[]>(json)!.Any(item => item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name)),
            "eventStories" => Utils.Deserialize<EventStory[]>(json)!.Any(item => item.Id <= 0 || item.EventId <= 0 ||
                item.EventStoryEpisodes == null || item.EventStoryEpisodes.Any(episode => episode == null || episode.Id <= 0)),
            "actionSets" => Utils.Deserialize<ActionSet[]>(json)!.Any(item => item.Id <= 0),
            "releaseConditions" => Utils.Deserialize<ReleaseCondition[]>(json)!.Any(item => item.Id <= 0),
            _ => throw new ArgumentOutOfRangeException(nameof(file))
        };
        if (invalid) throw new InvalidDataException("主数据缺少有效的 ID、活动标题或章节信息");
    }
}

using System.Text.RegularExpressions;
using SekaiToolsBase.DataList;

namespace SekaiToolsBase.Story;

public sealed record FlashbackEventOrigin(int EventId, int Number, string Name, int? EpisodeNumber = null)
{
    public string DisplayName => $"第 {Number} 期 · {Name}" + (EpisodeNumber is { } episode ? $" · 第 {episode} 话" : "");
}

/// <summary>通过主数据的解锁关系关联语音前缀与活动，避免将团内序号当作活动期数。</summary>
public sealed partial class FlashbackEventCatalog
{
    private readonly Dictionary<string, FlashbackEventOrigin> _origins = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<int>> _episodeNumbers;

    [GeneratedRegex(@"^areatalk_(ev|wl)_(.+)_\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex AreaPattern();

    public FlashbackEventCatalog(IEnumerable<GameEvent> events, IEnumerable<EventStory> stories,
        IEnumerable<ActionSet> actions, IEnumerable<ReleaseCondition> conditions)
    {
        var eventMap = events.Where(item => item != null && item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .DistinctBy(item => item.Id).ToDictionary(item => item.Id);
        var storyMap = stories.Where(item => item != null && item.Id > 0 && item.EventStoryEpisodes != null
                && eventMap.ContainsKey(item.EventId)).DistinctBy(item => item.Id).ToArray();
        _episodeNumbers = storyMap.GroupBy(story => story.EventId).ToDictionary(group => group.Key,
            group => group.SelectMany(story => story.EventStoryEpisodes).Where(episode => episode != null)
                .Select(episode => episode.EpisodeNo).Where(number => number > 0).ToHashSet());
        // EventStory.Id 是稳定的剧情活动期数；不能用当前缓存中的数组位置计算。
        var origins = storyMap.ToDictionary(item => item.Id,
            item => new FlashbackEventOrigin(item.EventId, item.Id, eventMap[item.EventId].Name));
        var byEvent = origins.Values.DistinctBy(item => item.EventId).ToDictionary(item => item.EventId);
        var episodes = storyMap.SelectMany(story => story.EventStoryEpisodes.Where(episode => episode != null)
                .Select(episode => (episode.Id, Origin: origins[story.Id])))
            .DistinctBy(item => item.Id).ToDictionary(item => item.Id, item => item.Origin);
        var actionMap = actions.Where(item => item != null).DistinctBy(item => item.Id).ToDictionary(item => item.Id);
        var conditionMap = conditions.Where(item => item != null).DistinctBy(item => item.Id).ToDictionary(item => item.Id);

        FlashbackEventOrigin? ResolveAction(int id, HashSet<int> visited)
        {
            if (!visited.Add(id) || !actionMap.TryGetValue(id, out var action) ||
                !conditionMap.TryGetValue(action.ReleaseConditionId, out var condition)) return null;
            return condition.ReleaseConditionType switch
            {
                "event_story" => episodes.GetValueOrDefault(condition.ReleaseConditionTypeId),
                "action_set" => ResolveAction(condition.ReleaseConditionTypeId, visited),
                _ => null
            };
        }

        void Add(string key, FlashbackEventOrigin origin)
        {
            // 同一前缀可能在后续活动再次解锁，采用最早的活动，与参考实现一致。
            if (!_origins.TryGetValue(key, out var previous) || origin.Number < previous.Number)
                _origins[key] = origin;
        }

        foreach (var action in actionMap.Values)
        {
            var match = AreaPattern().Match(action.ScenarioId ?? "");
            if (!match.Success || ResolveAction(action.Id, []) is not { } origin) continue;
            Add((match.Groups[1].Value == "wl" ? "wl_" : "") + match.Groups[2].Value, origin);
        }
        foreach (var story in storyMap)
            foreach (var episode in story.EventStoryEpisodes.Where(episode => episode != null))
            {
                var scenario = episode.ScenarioId ?? "";
                if (scenario.StartsWith("wl_", StringComparison.Ordinal) && scenario.LastIndexOf('_') is var end && end > 3)
                    Add(scenario[..end], origins[story.Id]);
            }
        // 参考项目记录的早期命名例外，无对应活动数据时不生成虚假的标题。
        foreach (var (key, eventId) in new[] { ("band_01", 1), ("shuffle_03", 9), ("night_", 53) })
            if (byEvent.TryGetValue(eventId, out var origin)) _origins[key] = origin;
    }

    public FlashbackEventOrigin? Resolve(string source)
    {
        if (GetEventKey(source) is not { } key || !_origins.TryGetValue(key, out var origin)) return null;
        var normalized = source.StartsWith("sc_", StringComparison.Ordinal) ? source[3..] : source;
        if (normalized.StartsWith("card_", StringComparison.Ordinal)) return origin;
        // 来源最后一段是语音章节号；卡面最后一段为星数和前后篇，不属于活动正文话数。
        var suffix = source[(source.LastIndexOf('_') + 1)..];
        if (!int.TryParse(suffix, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var voiceEpisode)) return origin;
        // 第 9 期语音从 00 开始，对应主数据中的第 1 话，沿用参考项目的命名修正。
        var episode = (long)voiceEpisode + (origin.EventId == 9 ? 1 : 0);
        return episode <= int.MaxValue && _episodeNumbers[origin.EventId].Contains((int)episode)
            ? origin with { EpisodeNumber = (int)episode } : origin;
    }

    public static string? GetEventKey(string source)
    {
        if (source.StartsWith("sc_", StringComparison.Ordinal)) source = source[3..];
        if (source.StartsWith("card_", StringComparison.Ordinal))
        {
            source = source[5..];
            var rarity = source.LastIndexOf('_');
            if (rarity <= 0) return null;
            source = source[..rarity];
            var character = source.LastIndexOf('_');
            if (character <= 0) return null;
            source = source[..character];
            return source.StartsWith("ev_", StringComparison.Ordinal) ? source[3..] : null;
        }
        if (!source.StartsWith("ev_", StringComparison.Ordinal)) return null;
        source = source[3..];
        // 部分卡面语音没有 card_ 前缀。
        if (Regex.IsMatch(source, @"_\d+[ab]$", RegexOptions.CultureInvariant))
            return GetEventKey("card_ev_" + source);
        var chapter = source.LastIndexOf('_');
        return chapter > 0 ? source[..chapter] : null;
    }
}

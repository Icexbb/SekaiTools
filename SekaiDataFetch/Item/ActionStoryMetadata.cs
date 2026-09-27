using System.Text.RegularExpressions;
using SekaiToolsBase.DataList;

namespace SekaiDataFetch.Item;

public record ActionStoryActivity(int Id, int Number, string Name)
{
    public string DisplayName => $"第 {Number} 期 · {Name}（ID {Id}）";
}

public static class ActionStoryMetadata
{
    public static void Apply(IReadOnlyList<AreaStorySet> stories, IEnumerable<ActionSet> actions,
        IEnumerable<ReleaseCondition> conditions, IEnumerable<EventStory> eventStories, IEnumerable<GameEvent> events)
    {
        var actionArray = actions.ToArray();
        var conditionArray = conditions.ToArray();
        var storyArray = eventStories.ToArray();
        var eventArray = events.ToArray();
        if (actionArray.Any(action => action == null) || conditionArray.Any(condition => condition == null)
            || eventArray.Any(@event => @event == null) || storyArray.Any(story => story == null
                || story.EventStoryEpisodes == null || story.EventStoryEpisodes.Any(episode => episode == null)))
            throw new ArgumentException("活动元数据包含空条目或无效章节列表");
        var actionsById = actionArray.ToDictionary(action => action.Id);
        var conditionsById = conditionArray.ToDictionary(condition => condition.Id);
        var eventsById = eventArray.ToDictionary(@event => @event.Id);
        var orderedStories = storyArray.Where(story => story.EventStoryEpisodes.Length > 0 && eventsById.ContainsKey(story.EventId))
            .OrderBy(story => story.Id).ToArray();
        var activities = orderedStories.Select((story, index) =>
                new ActionStoryActivity(story.EventId, index + 1, eventsById[story.EventId].Name))
            .DistinctBy(activity => activity.Id).ToDictionary(activity => activity.Id);
        var episodes = orderedStories.SelectMany(story => story.EventStoryEpisodes.Select(episode =>
            (episode.Id, Activity: activities[story.EventId]))).ToDictionary(episode => episode.Id, episode => episode.Activity);
        var resolved = new Dictionary<int, ActionStoryActivity?>();

        ActionStoryActivity? Resolve(int actionId, HashSet<int> visited)
        {
            if (resolved.TryGetValue(actionId, out var cached)) return cached;
            if (!visited.Add(actionId) || !actionsById.TryGetValue(actionId, out var action)
                || !conditionsById.TryGetValue(action.ReleaseConditionId, out var condition)) return null;
            var activity = condition.ReleaseConditionType switch
            {
                "event_story" => episodes.GetValueOrDefault(condition.ReleaseConditionTypeId),
                "action_set" => Resolve(condition.ReleaseConditionTypeId, visited),
                _ => null
            };
            resolved[actionId] = activity;
            return activity;
        }

        var additions = new Dictionary<int, ActionStoryActivity?>();
        var initialIds = new HashSet<int>();
        var hasActivityAnchor = actionsById.Values.Any(action => action.ActionSetType == "normal"
            && ClassifyBatch(action).Key == "other" && Resolve(action.Id, []) != null);
        ActionStoryActivity? latestActivity = null;
        foreach (var action in actionsById.Values.OrderBy(action => action.Id))
        {
            var release = Resolve(action.Id, []);
            var batch = ClassifyBatch(action);
            // Named special batches are independent of the normal activity update sequence.
            if (batch.Key == "other" && action.ActionSetType == "normal")
            {
                if (release != null && (latestActivity == null || release.Number > latestActivity.Number))
                    latestActivity = release;
                additions[action.Id] = latestActivity;
                if (latestActivity == null && hasActivityAnchor) initialIds.Add(action.Id);
            }
        }

        foreach (var story in stories)
        {
            story.ReleaseActivity = Resolve(story.ActionSet.Id, []);
            story.AdditionActivity = additions.GetValueOrDefault(story.ActionSet.Id);
            var batch = ClassifyBatch(story.ActionSet);
            if (initialIds.Contains(story.ActionSet.Id)) batch = ("initial", "初始对话（推断）");
            story.BatchKey = batch.Key;
            story.BatchName = batch.Name;
        }
    }

    public static (string Key, string Name) ClassifyBatch(ActionSet action)
    {
        var monthly = Regex.Match(action.ScenarioId, @"monthly_?(\d{4})(?:_|$)", RegexOptions.IgnoreCase);
        if (monthly.Success && int.TryParse(monthly.Groups[1].Value[2..], out var month) && month is >= 1 and <= 12)
        {
            var date = monthly.Groups[1].Value;
            return ($"monthly:{date}", $"每月对话 · 20{date[..2]}-{date[2..]}");
        }
        var april = Regex.Match(action.ScenarioId, @"aprilfool_?(\d{4})(?:_|$)", RegexOptions.IgnoreCase);
        if (april.Success) return ($"aprilfool:{april.Groups[1].Value}", $"愚人节 · {april.Groups[1].Value}");
        var anniversary = Regex.Match(action.ScenarioId, @"(?:^|_)(\d+)(?:st|nd|rd|th)aniv(?:_|$)", RegexOptions.IgnoreCase);
        if (anniversary.Success) return ($"anniversary:{anniversary.Groups[1].Value}", $"{anniversary.Groups[1].Value} 周年对话");
        if (action.ActionSetType == "limited") return ("limited", "其他限定对话");
        return ("other", "其他更新／未归类");
    }
}

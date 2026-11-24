using System.Text;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiDataFetch;

public sealed record DownloadCandidate(string Tag, string Url, string SearchText,
    Func<SourceData, string>? ResolveUrl = null)
{
    public bool Matches(string query)
    {
        return DownloadSearch.Matches(query, SearchText + " " + Tag + " " + Url);
    }
}

public static class DownloadSearch
{
    public static bool Matches(string? query, string text)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var normalized = text.Normalize(NormalizationForm.FormKC);
        return query.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

public static class StoryDownloadCandidates
{
    public static IEnumerable<DownloadCandidate> Event(EventStorySet story, SourceData source)
    {
        return story.EventStory.EventStoryEpisodes.Select(episode => Create(source,
            $"{source.SourceName}|EventStory|{story.Index}-{story.GameEvent.Name}|{episode.EpisodeNo}-{episode.Title}",
            story.EventStory.EventId.ToString(),
            candidate => new SourceList(candidate).EventStory(episode.ScenarioId, story.EventStory.AssetBundleName)));
    }

    public static IEnumerable<DownloadCandidate> Card(CardStorySet story, SourceData source)
    {
        return new[] { ("前篇", story.FirstPart), ("后篇", story.SecondPart) }.Select(part => Create(source,
            $"{source.SourceName}|CardStory|{story.Card.Id}-{story.Card.Prefix}|{part.Item1}",
            "", candidate => new SourceList(candidate).MemberStory(part.Item2)));
    }

    public static IEnumerable<DownloadCandidate> Unit(UnitStorySet.Chapter chapter, SourceData source)
    {
        return chapter.Episodes.Select(episode => Create(source,
            $"{source.SourceName}|UnitStory|{chapter.Name}|{episode.EpisodeNoLabel}-{episode.Title}",
            "", candidate => new SourceList(candidate).UnitStory(episode.ScenarioId, chapter.AssetBundleName)));
    }

    public static IEnumerable<DownloadCandidate> Special(SpecialStorySet story, SourceData source)
    {
        return story.Episodes.Select(episode => Create(source,
            $"{source.SourceName}|SpecialStory|{story.SpecialStory.AssetBundleName}-{story.SpecialStory.Title}|{episode.ScenarioId}-{episode.Title}",
            "", candidate => new SourceList(candidate).SpecialStory(episode)));
    }

    public static DownloadCandidate Action(AreaStorySet story, SourceData source)
    {
        return Create(source,
            $"{source.SourceName}|ActionSet|{story.ActionSet.Id}-{story.ActionSet.ScriptId}",
            $"{story.TalkId} {story.AreaName} {story.TypeName} {story.BatchName} {story.ReleaseActivity?.DisplayName} {story.AdditionActivity?.DisplayName}",
            candidate => new SourceList(candidate).ActionSet(story));
    }

    private static DownloadCandidate Create(SourceData source, string tag, string searchText, Func<SourceData, string> resolve) =>
        new(tag, resolve(source), searchText, resolve);
}

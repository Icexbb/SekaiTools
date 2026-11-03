using System.Text;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiDataFetch;

public sealed record DownloadCandidate(string Tag, string Url, string SearchText)
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
        var urls = new SourceList(source);
        return story.EventStory.EventStoryEpisodes.Select(episode => new DownloadCandidate(
            $"{source.SourceName}|EventStory|{story.Index}-{story.GameEvent.Name}|{episode.EpisodeNo}-{episode.Title}",
            urls.EventStory(episode.ScenarioId, story.EventStory.AssetBundleName),
            story.EventStory.EventId.ToString()));
    }

    public static IEnumerable<DownloadCandidate> Card(CardStorySet story, SourceData source)
    {
        var urls = new SourceList(source);
        return new[] { ("前篇", story.FirstPart), ("后篇", story.SecondPart) }.Select(part => new DownloadCandidate(
            $"{source.SourceName}|CardStory|{story.Card.Id}-{story.Card.Prefix}|{part.Item1}",
            urls.MemberStory(part.Item2), ""));
    }

    public static IEnumerable<DownloadCandidate> Unit(UnitStorySet.Chapter chapter, SourceData source)
    {
        return chapter.Episodes.Select(episode => new DownloadCandidate(
            $"{source.SourceName}|UnitStory|{chapter.Name}|{episode.EpisodeNoLabel}-{episode.Title}",
            new SourceList(source).UnitStory(episode.ScenarioId, chapter.AssetBundleName), ""));
    }

    public static IEnumerable<DownloadCandidate> Special(SpecialStorySet story, SourceData source)
    {
        return story.Episodes.Select(episode => new DownloadCandidate(
            $"{source.SourceName}|SpecialStory|{story.SpecialStory.AssetBundleName}-{story.SpecialStory.Title}|{episode.ScenarioId}-{episode.Title}",
            new SourceList(source).SpecialStory(episode), ""));
    }

    public static DownloadCandidate Action(AreaStorySet story, SourceData source)
    {
        return new DownloadCandidate(
            $"{source.SourceName}|ActionSet|{story.ActionSet.Id}-{story.ActionSet.ScriptId}",
            new SourceList(source).ActionSet(story),
            $"{story.TalkId} {story.AreaName} {story.TypeName} {story.BatchName} {story.ReleaseActivity?.DisplayName} {story.AdditionActivity?.DisplayName}");
    }
}
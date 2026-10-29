using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class DownloadSearchTests
{
    [Theory]
    [InlineData("", "abc", true)]
    [InlineData(" ＡＢＣ １２３ ", "abc chapter 123", true)]
    [InlineData("天空 42", "42-天空の物語", true)]
    [InlineData("天空 43", "42-天空の物語", false)]
    [InlineData("S0001", "ID 77 TalkId S0001", true)]
    public void NormalizesWidthAndCaseButRequiresEverySearchTerm(string query, string text, bool expected) =>
        Assert.Equal(expected, DownloadSearch.Matches(query, text));

    [Fact]
    public void EventSearchIncludesActualEventIdAndOnlyMatchingEpisodeIsSelected()
    {
        var story = new EventStorySet(new EventStory
        {
            EventId = 777,
            AssetBundleName = "event-pack",
            EventStoryEpisodes = [new() { EpisodeNo = 1, Title = "First", ScenarioId = "script_one" },
                new() { EpisodeNo = 2, Title = "Second", ScenarioId = "script_two" }]
        }, new GameEvent { Name = "Sky" }, 123);
        var candidates = StoryDownloadCandidates.Event(story, SourceData.Default[0]).ToArray();
        Assert.Equal(2, candidates.Count(candidate => candidate.Matches("777")));
        Assert.Single(candidates, candidate => candidate.Matches("777 second"));
        Assert.Single(candidates, candidate => candidate.Matches("SCRIPT_TWO"));
        Assert.Equal(2, story.EventStory.EventStoryEpisodes.Length);
    }

    [Fact]
    public void CardUnitAndSpecialExposeFileLevelResultsWithoutChangingSourceRouting()
    {
        var source = SourceData.Default[0];
        var urls = new SourceList(source);
        var card = new CardStorySet(new Card { Id = 42, Prefix = "Bright" },
            new CardEpisode { ScenarioId = "first", AssetBundleName = "front" },
            new CardEpisode { ScenarioId = "second", AssetBundleName = "back" });
        var cards = StoryDownloadCandidates.Card(card, source).ToArray();
        Assert.Equal(2, cards.Length);
        Assert.Equal(urls.MemberStory(card.FirstPart), cards[0].Url);
        Assert.Single(cards, candidate => candidate.Matches("42 后篇"));
        var chapter = new UnitStorySet.Chapter(new UnitChapter
        { Title = "Start", AssetBundleName = "unit", Episodes = [new UnitEpisode { Title = "Opening", ScenarioId = "opening", EpisodeNoLabel = "1" }] });
        Assert.Single(StoryDownloadCandidates.Unit(chapter, source), candidate => candidate.Matches("start opening"));
        var special = new SpecialStorySet(new SpecialStory
        {
            AssetBundleName = "special",
            Title = "Festival",
            Episodes =
            [new SpecialStoryEpisode { Title = "Morning", ScenarioId = "morning", AssetBundleName = "episode" }]
        });
        var candidate = Assert.Single(StoryDownloadCandidates.Special(special, source));
        Assert.Equal(urls.SpecialStory(special.Episodes[0]), candidate.Url);
        Assert.True(candidate.Matches("festival morning"));
    }

    [Fact]
    public void TalkIdSearchNeverReplacesOriginalIdBasedDownloadAddress()
    {
        var source = SourceData.Default[0];
        var story = new AreaStorySet(new ActionSet { Id = 1234, AreaId = 1, ScenarioId = "scenario", ScriptId = "script" })
        { TalkId = "S0001" };
        var candidate = StoryDownloadCandidates.Action(story, source);
        Assert.True(candidate.Matches("s0001"));
        Assert.Equal(new SourceList(source).ActionSet(story), candidate.Url);
        Assert.Contains("1234", candidate.Tag);
    }
}

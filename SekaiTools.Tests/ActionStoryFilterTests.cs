using SekaiDataFetch.Item;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class ActionStoryFilterTests
{
    private static AreaStorySet Story(int id, int area, params int[] characters) =>
        new(new ActionSet { Id = id, AreaId = area }) { CharacterIds = characters };

    [Fact]
    public void AllAreasIncludesDifferentLocations()
    {
        var stories = new[] { Story(10, 1, 1), Story(11, 2, 2) };
        Assert.Equal(2, stories.Count(new ActionStoryFilter().Matches));
        Assert.Single(stories, new ActionStoryFilter(AreaId: 2).Matches);
    }

    [Fact]
    public void FiltersIntersectAndIdBoundsAreInclusive()
    {
        var filter = new ActionStoryFilter(AreaId: 2, CharacterId: 3, MinimumId: 10, MaximumId: 20);
        Assert.True(filter.Matches(Story(10, 2, 3)));
        Assert.True(filter.Matches(Story(20, 2, 3)));
        Assert.False(filter.Matches(Story(9, 2, 3)));
        Assert.False(filter.Matches(Story(21, 2, 3)));
        Assert.False(filter.Matches(Story(15, 1, 3)));
        Assert.False(filter.Matches(Story(15, 2, 4)));
    }

    [Fact]
    public void AllAreasStillRespectsCharacterAndOneSidedRange()
    {
        var filter = new ActionStoryFilter(CharacterId: 3, MinimumId: 10);
        Assert.True(filter.Matches(Story(50, 99, 3)));
        Assert.False(filter.Matches(Story(50, 99, 4)));
        Assert.False(filter.Matches(Story(9, 99, 3)));
        Assert.True(new ActionStoryFilter(MaximumId: 10).Matches(Story(10, 1)));
    }
    private static AreaStorySet TalkStory(int id, string? talkId) =>
        new(new ActionSet { Id = id, AreaId = 2 }) { TalkId = talkId, CharacterIds = [3] };

    [Fact]
    public void TalkIdRangeUsesSequentialNumberAndIntersectsOtherFilters()
    {
        var filter = new ActionStoryFilter(AreaId: 2, CharacterId: 3, MinimumId: 2646, MaximumId: 2649,
            IdIndex: ActionStoryIdIndex.TalkId);
        Assert.True(filter.Matches(TalkStory(9000, "2646")));
        Assert.True(filter.Matches(TalkStory(9001, "2649")));
        Assert.False(filter.Matches(TalkStory(2646, "2650")));
        Assert.False(filter.Matches(TalkStory(9002, "2645")));
        Assert.False(filter.Matches(TalkStory(9003, "S2646")));
        Assert.False(filter.Matches(TalkStory(9004, null)));
        Assert.False((filter with { AreaId = 1 }).Matches(TalkStory(9000, "2646")));
        Assert.False((filter with { CharacterId = 4 }).Matches(TalkStory(9000, "2646")));
        Assert.True(new ActionStoryFilter(MinimumId: 2646, MaximumId: 2649).Matches(TalkStory(2646, "2650")));
    }

    [Fact]
    public void SpecialTalkIdSupportsOneSidedRangesWithoutMatchingNormalTalkIds()
    {
        var filter = new ActionStoryFilter(MaximumId: 2, IdIndex: ActionStoryIdIndex.TalkId, SpecialTalkId: true);
        Assert.True(filter.Matches(TalkStory(9000, "S0001")));
        Assert.True(filter.Matches(TalkStory(9001, "S0002")));
        Assert.False(filter.Matches(TalkStory(9002, "S0003")));
        Assert.False(filter.Matches(TalkStory(9003, "0001")));
        Assert.True((filter with { MinimumId = 2, MaximumId = null }).Matches(TalkStory(9002, "S0003")));
    }

    [Fact]
    public void TalkIdWithoutBoundsIncludesBothKindsAndSortsNumbersNumerically()
    {
        var filter = new ActionStoryFilter(IdIndex: ActionStoryIdIndex.TalkId);
        var stories = new[] { TalkStory(1, "S0001"), TalkStory(2, "0010"), TalkStory(3, "0002"), TalkStory(4, null) };
        Assert.All(stories, story => Assert.True(filter.Matches(story)));
        Assert.Equal(new[] { 3, 2, 1, 4 },
            stories.OrderBy(filter.GetSortKey).Select(story => story.ActionSet.Id));
    }

    [Theory]
    [InlineData("0001", 1, false)]
    [InlineData("S0002", 2, true)]
    [InlineData(" s0003 ", 3, true)]
    public void TalkIdParserAcceptsNormalAndSpecialIds(string text, int expected, bool special)
    {
        Assert.True(ActionStoryFilter.TryParseTalkId(text, out var number, out var parsedSpecial));
        Assert.Equal(expected, number);
        Assert.Equal(special, parsedSpecial);
    }

    [Theory]
    [InlineData("")]
    [InlineData("S")]
    [InlineData("0")]
    [InlineData("S0000")]
    [InlineData("-1")]
    [InlineData("1.2")]
    [InlineData("S-1")]
    [InlineData("2147483648")]
    public void TalkIdParserRejectsInvalidNumbers(string text)
    {
        Assert.False(ActionStoryFilter.TryParseTalkId(text, out _, out _));
    }
}

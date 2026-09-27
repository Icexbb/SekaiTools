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
}

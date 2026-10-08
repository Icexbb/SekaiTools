using SekaiDataFetch.List;
using SekaiDataFetch.Source;

namespace SekaiTools.Tests;

public class SourceCacheTests
{
    private static SourceData Source(string name, string template) => new()
    {
        SourceName = name,
        SourceTemplate = template,
        StorageBaseUrl = "",
        ActionSetTemplate = "",
        MemberStoryTemplate = "",
        EventStoryTemplate = "",
        SpecialStoryTemplate = "",
        UnitStoryTemplate = ""
    };

    [Fact]
    public void DifferentListSourcesHaveSeparateCachesEvenWithSameName()
    {
        var first = Source("same", "source-a/{type}.json");
        var second = Source("same", "source-b/{type}.json");
        Assert.NotEqual(BaseListStory.GetCacheDirectory(first), BaseListStory.GetCacheDirectory(second));
    }

    [Fact]
    public void CacheIdentityIsStableAcrossReloadsAndDisplayNameChanges()
    {
        var first = Source("original", "source/{type}.json");
        var renamed = Source("renamed", "source/{type}.json");
        Assert.Equal(BaseListStory.GetCacheDirectory(first), BaseListStory.GetCacheDirectory(renamed));
        Assert.Equal(Path.Combine(BaseListStory.DataBaseDir, "Data", "cache"),
            Path.GetDirectoryName(BaseListStory.GetCacheDirectory(first)));
    }
}

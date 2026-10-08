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

    [Theory]
    [InlineData("Sekai Best")]
    [InlineData("Haruki NEO")]
    [InlineData("中文数据源")]
    public void CacheDirectoryUsesSourceName(string name)
    {
        Assert.Equal(Path.Combine(BaseListStory.DataBaseDir, "Data", "cache", name),
            BaseListStory.GetCacheDirectory(Source(name, "source/{type}.json")));
    }

    [Fact]
    public void DifferentSourceNamesHaveSeparateCaches()
    {
        Assert.NotEqual(BaseListStory.GetCacheDirectory(Source("first", "source/{type}.json")),
            BaseListStory.GetCacheDirectory(Source("second", "source/{type}.json")));
        Assert.Equal(BaseListStory.GetCacheDirectory(Source("same", "first/{type}.json")),
            BaseListStory.GetCacheDirectory(Source("same", "second/{type}.json")));
    }

    [Theory]
    [InlineData("../source", ".._source")]
    [InlineData("a\\b", "a_b")]
    [InlineData("a:b?*", "a_b__")]
    [InlineData("..", "未命名数据源")]
    [InlineData(" ", "未命名数据源")]
    [InlineData("CON", "_CON")]
    [InlineData("nul.json", "_nul.json")]
    [InlineData("COM1", "_COM1")]
    public void InvalidSourceNamesRemainInsideCacheDirectory(string name, string directory)
    {
        var path = BaseListStory.GetCacheDirectory(Source(name, "source/{type}.json"));
        Assert.Equal(directory, Path.GetFileName(path));
        Assert.Equal(Path.Combine(BaseListStory.DataBaseDir, "Data", "cache"), Path.GetDirectoryName(path));
    }
}

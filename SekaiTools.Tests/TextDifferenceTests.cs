using SekaiToolsBase.Story.Translation;

namespace SekaiTools.Tests;

public class TextDifferenceTests
{
    [Theory]
    [InlineData("相同文本", "相同文本")]
    [InlineData("", "")]
    [InlineData("第一行\r\n第二行", "第一行\n第二行")]
    public void EquivalentTextHasNoHighlight(string current, string reference)
    {
        var result = TextDifference.Compare(current, reference);
        Assert.Empty(result.Current);
        Assert.Empty(result.Reference);
    }

    [Fact]
    public void ReplacementKeepsSurroundingCommonTextUnmarked()
    {
        var result = TextDifference.Compare("今天一起练习吧！", "今天一起休息吧！");
        Assert.Equal([new TextDifferenceRange(4, 2)], result.Current);
        Assert.Equal([new TextDifferenceRange(4, 2)], result.Reference);
    }

    [Theory]
    [InlineData("你好！", "你好呀！", 2, 0, 2, 1)]
    [InlineData("你好呀！", "你好！", 2, 1, 2, 0)]
    [InlineData("", "整段参考", 0, 0, 0, 4)]
    [InlineData("整段译文", "", 0, 4, 0, 0)]
    public void InsertionsAndDeletionsKeepBoundaryMarkers(string current, string reference,
        int currentStart, int currentLength, int referenceStart, int referenceLength)
    {
        var result = TextDifference.Compare(current, reference);
        Assert.Equal([new TextDifferenceRange(currentStart, currentLength)], result.Current);
        Assert.Equal([new TextDifferenceRange(referenceStart, referenceLength)], result.Reference);
    }

    [Fact]
    public void SeparateEditsAndExplicitLineBreaksRemainDistinct()
    {
        var result = TextDifference.Compare("早上好\n一起练习吧！", "晚上好，一起休息吧！");
        Assert.Equal([new(0, 1), new(3, 1), new TextDifferenceRange(6, 2)], result.Current);
        Assert.Equal([new(0, 1), new(3, 1), new TextDifferenceRange(6, 2)], result.Reference);
    }

    [Theory]
    [InlineData("a👩‍👩‍👧‍👦b", "a🙂b", 1, 11, 1, 2)]
    [InlineData("ae\u0301b", "aéb", 1, 2, 1, 1)]
    public void DifferencesNeverSplitTextElements(string current, string reference,
        int currentStart, int currentLength, int referenceStart, int referenceLength)
    {
        var result = TextDifference.Compare(current, reference);
        Assert.Equal([new TextDifferenceRange(currentStart, currentLength)], result.Current);
        Assert.Equal([new TextDifferenceRange(referenceStart, referenceLength)], result.Reference);
    }

    [Fact]
    public void LargeUnrelatedPasteFallsBackToBoundedMiddleComparison()
    {
        var result = TextDifference.Compare("前" + new string('甲', 2000) + "后",
            "前" + new string('乙', 2000) + "后");
        Assert.Equal([new TextDifferenceRange(1, 2000)], result.Current);
        Assert.Equal([new TextDifferenceRange(1, 2000)], result.Reference);
    }
}

using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.Tests;

public class DialogLookaheadEvidenceTests
{
    [Theory]
    [InlineData("（……次が、ラストのソロ）", "（……熱いな）", "（……次が、")]
    [InlineData("（東雲くんと青柳くんが）", "（……次が、ラストのソロ）", "（東雲く")]
    [InlineData("（", "（まだ続く）", "")]
    [InlineData("同じ一行\n別の二行目", "同じ一行\n以前の二行目", "")]
    [InlineData("abcdefghijklX", "abcdefghijklY", "")]
    [InlineData("", "別の行", "")]
    public void SameSpeakerNeedsEvidenceBeyondSharedFirstLinePrefix(string body, string previous, string expected)
    {
        Assert.Equal(expected, DialogTemplateMatcher.LookaheadContentPrefix(
            Dialog("こはね", body), [Dialog("こはね", previous)]));
    }

    [Fact]
    public void VerificationDistinguishesAllSkippedTargetsAndLastVisibleLine()
    {
        Assert.Equal("（……次が、ラ", DialogTemplateMatcher.LookaheadContentPrefix(
            Dialog("こはね", "（……次が、ラストのソロ）"),
            [Dialog("こはね", "（……熱いな）"), Dialog("こはね", "（……次の曲）")]));
    }

    [Fact]
    public void NamesWithSameRenderedPrefixStillRequireDistinctiveContent()
    {
        Assert.Equal("（……次が）", DialogTemplateMatcher.LookaheadContentPrefix(
            Dialog("こはねA", "（……次が）"), [Dialog("こはねB", "（……熱いな）")]));
    }

    [Fact]
    public void DifferentSpeakerCanConfirmOrdinaryPrefix()
    {
        Assert.Equal("（……", DialogTemplateMatcher.LookaheadContentPrefix(
            Dialog("こはね", "（……次が）"), [Dialog("杏", "（……熱いな）")]));
    }

    private static DialogStoryEvent Dialog(string speaker, string body) => new(0, body, 0, speaker, false, false);
}

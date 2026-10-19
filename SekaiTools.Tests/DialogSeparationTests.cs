using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class DialogSeparationTests
{
    [Theory]
    [InlineData(".", 1d / 3)]
    [InlineData("...", 1)]
    [InlineData("......", 2)]
    [InlineData("A1", 1)]
    [InlineData("中文......A1", 5)]
    public void LengthUsesThirdsForPeriods(string content, double expected)
    {
        Assert.Equal(expected, content.DialogContentLength(), 12);
    }

    [Theory]
    [InlineData(35, "......", false)]
    [InlineData(35, ".......", true)]
    [InlineData(36, "...", false)]
    [InlineData(37, ".", true)]
    [InlineData(38, "", true)]
    public void ThreeLineDialogOnlyDefaultsToSeparationAbove37(int count, string suffix, bool expected)
    {
        var set = CreateSet("一\n二\n三", new string('中', count) + suffix);
        Assert.Equal(expected, set.SeparatorNeeded);
        Assert.Equal(expected, set.SeparatorEnabled);
        Assert.True(set.SeparatorUsable);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("中", false)]
    [InlineData("短句......", true)]
    public void ShortDialogsRetainManualSeparationWhenPossible(string content, bool usable)
    {
        var set = CreateSet("一\n二\n三", content);
        Assert.False(set.SeparatorEnabled);
        Assert.Equal(usable, set.SeparatorUsable);
    }

    [Fact]
    public void TwoLineOriginalDoesNotEnableSeparationForLongTranslation()
    {
        var set = CreateSet("一\n二", new string('中', 50));
        Assert.False(set.SeparatorUsable);
        Assert.False(set.SeparatorEnabled);
    }

    [Theory]
    [InlineData("短\n较长的后半句", 1)]
    [InlineData("短\\R较长的后半句", 1)]
    public void ExplicitSplitUsesFirstPartLength(string content, int expected)
    {
        Assert.Equal(expected, CreateSet("一\n二\n三", content).Separate.SeparatorContentIndex);
    }

    private static DialogBaseFrameSet CreateSet(string original, string translated)
    {
        var data = new DialogStoryEvent(0, original, 0, "角色", false, false);
        data.SetTranslation("角色", translated);
        return new DialogBaseFrameSet(data, new FrameRate(30));
    }
}

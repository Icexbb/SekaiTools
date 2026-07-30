using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class DialogTimingCheckTests
{
    [Fact]
    public void 普通对话时长不足会报告本行问题()
    {
        var set = CreateSet("一二三四五六七八九十", 5);

        var issue = Assert.Single(DialogTimingCheck.GetIssues(set, 80));

        Assert.Equal("本行", issue.LineName);
        Assert.Equal(800, issue.RequiredMilliseconds);
        Assert.Equal(500, issue.AvailableMilliseconds);
    }

    [Fact]
    public void 分行对话分别检查每段时长()
    {
        var set = CreateSet("一二三四五六七八九十", 10);
        set.UseSeparator = true;
        set.SetSeparator(2, 5);

        var issue = Assert.Single(DialogTimingCheck.GetIssues(set, 80));

        Assert.Equal("第一行", issue.LineName);
        Assert.Equal(400, issue.RequiredMilliseconds);
        Assert.Equal(200, issue.AvailableMilliseconds);
    }

    [Fact]
    public void CharTime为零时不报告问题()
    {
        var set = CreateSet("一二三四五六七八九十", 1);

        Assert.Empty(DialogTimingCheck.GetIssues(set, 0));
    }

    [Fact]
    public void 时长刚好满足字数乘CharTime时不报告问题()
    {
        var set = CreateSet("一二三四五六七八九十", 8);

        Assert.Empty(DialogTimingCheck.GetIssues(set, 80));
    }

    private static DialogBaseFrameSet CreateSet(string content, int frameCount)
    {
        var data = new DialogStoryEvent(0, content, 0, "角色", false, false);
        data.SetTranslation("角色", content);
        var set = new DialogBaseFrameSet(data, new FrameRate(10));
        for (var index = 1; index <= frameCount; index++) set.Add(index, Point.Empty);
        return set;
    }
}
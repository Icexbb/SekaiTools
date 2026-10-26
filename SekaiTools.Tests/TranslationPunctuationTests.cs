using SekaiToolsBase.GameScript;
using SekaiToolsGUI.ViewModel.Translate;
using Script = SekaiToolsBase.GameScript.GameScript;

namespace SekaiTools.Tests;

public class TranslationPunctuationTests
{
    [Theory]
    [InlineData("你好？")]
    [InlineData("你好！")]
    [InlineData("你好?")]
    [InlineData("你好!")]
    [InlineData("你好？！")]
    [InlineData("「你好？」")]
    [InlineData("（你好！）")]
    [InlineData("「（你好！）」")]
    [InlineData("你好！  ")]
    public void 问号感叹号作为结束标点不会警告(string text)
    {
        Assert.Empty(CreateLine(text).Check);
    }

    [Theory]
    [InlineData("你好")]
    [InlineData("「你好」")]
    [InlineData("（你好）")]
    public void 缺少结束标点仍显示警告(string text)
    {
        Assert.Contains("句尾缺少结束标点", CreateLine(text).Check);
    }

    private static LineDialogModel CreateLine(string text)
    {
        var model = new TranslatePageModel();
        model.LoadScript(new Script
        {
            TalkData = [new Talk("角色", "原文", 0)],
            Snippets = [new Snippet { Action = 1 }]
        }, "script.json");
        var line = Assert.IsType<LineDialogModel>(Assert.Single(model.Events));
        line.Content.Translated = text;
        return line;
    }
}

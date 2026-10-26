using System.Text.Json;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using GameScriptModel = SekaiToolsBase.GameScript.GameScript;

namespace SekaiTools.Tests;

public class StoryParsingTests
{
    [Theory]
    [InlineData("角色：一句", "对话数量不匹配")]
    [InlineData("角色：一句\n角色：二句", "特效数量不匹配")]
    [InlineData("横幅\n角色：一句\n角色：二句", "第 1 个条目类型不匹配")]
    [InlineData("角色：一句\n角色：二句\n横幅", null)]
    public void 旧版翻译返回具体不匹配原因(string content, string? expectedReason)
    {
        var script = new GameScriptModel
        {
            TalkData = [new Talk("角色", "一句", 0), new Talk("角色", "二句", 0)],
            SpecialEffectData = [new SpecialEffect(8, "", "", 0, 0)],
            Snippets = [new Snippet { Action = 1 }, new Snippet { Action = 1 }, new Snippet { Action = 6 }]
        };
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            var translation = new TranslationData(path);
            var reason = translation.GetMismatchReason(script);
            if (expectedReason == null) Assert.Null(reason);
            else Assert.Contains(expectedReason, reason);
            Assert.Equal(expectedReason == null, translation.IsApplicable(script));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Story忽略缺少TalkData的对话片段()
    {
        var script = new GameScriptModel
        {
            Snippets = [new Snippet { Action = 1 }],
            TalkData = [],
            SpecialEffectData = []
        };

        var story = new Story(script, new TranslationData(null));

        Assert.Empty(story.Events);
    }

    [Fact]
    public void 首句之前的震动特效不会产生负索引()
    {
        var source = new GameScriptModel
        {
            Snippets = [new Snippet { Action = 6 }],
            TalkData = [],
            SpecialEffectData = [new SpecialEffect(6, "", "", 20, 0)]
        };
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(source));
            var parsed = new GameScriptModel(path);

            Assert.Empty(parsed.TalkData);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 翻译文件解析对话特效和省略号()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "# 注释\n初音未来：你好……\n地点横幅");
            var translations = new TranslationData(path);

            Assert.Collection(translations.Translations,
                item =>
                {
                    var dialog = Assert.IsType<DialogTranslate>(item);
                    Assert.Equal("初音未来", dialog.Chara);
                    Assert.Equal("你好......", dialog.Body);
                },
                item => Assert.IsType<EffectTranslate>(item));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

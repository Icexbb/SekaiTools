using System.Text.Json;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using GameScriptModel = SekaiToolsBase.GameScript.GameScript;

namespace SekaiTools.Tests;

public class StoryParsingTests
{
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

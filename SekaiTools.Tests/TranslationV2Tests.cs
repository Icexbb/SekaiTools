using System.Text.Json;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using Script = SekaiToolsBase.GameScript.GameScript;

namespace SekaiTools.Tests;

public class TranslationV2Tests
{
    [Fact]
    public void LegacyAtSignTextStillLoadsAsLegacy()
    {
        var data = Load("角色：你好\n@普通效果文本");
        Assert.False(data.IsV2);
        Assert.Equal("@普通效果文本", data.Translations[1].Body);
    }

    [Fact]
    public void EditorDefaultsToV2AndBothExportsTrimDialogue()
    {
        var model = new SekaiToolsGUI.ViewModel.Translate.TranslatePageModel();
        model.LoadScript(CreateScript(), "renamed.json");
        var dialog = Assert.IsType<SekaiToolsGUI.ViewModel.Translate.LineDialogModel>(model.Events[1]);
        dialog.Content.Translated = " \n 第一行\n第二行 \n ";
        Assert.StartsWith("@version:2\n@scenario:scenario", model.Export());
        Assert.Equal("第一行\n第二行", new Story(CreateScript(), Load(model.Export())).Events[1].FinalContent);
        Assert.Contains("原名：第一行\\N第二行", model.Export(false));
        model.LoadTranslation(Load("@version:2\n@scenario:scenario\n@dialog:001[]\n@banner:005"), "empty.txt");
        Assert.Equal("", new Story(CreateScript(), Load(model.Export())).Events[1].FinalContent);
        Assert.Contains("@dialog:001[]", model.Export());
    }

    private static Script CreateScript() => new()
    {
        ScenarioId = "scenario",
        TalkData = [new Talk("原名", "原文", 0)],
        SpecialEffectData = [new SpecialEffect(8, "横幅", "", 0, 0)],
        OriginalEffectIndices = [4],
        Snippets = [new Snippet { Action = 6 }, new Snippet { Action = 1 }]
    };

    private static TranslationData Load(string content)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, content); return new TranslationData(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReorderedEntriesMatchIdentifiersAndPreserveBody()
    {
        var data = Load("@version:2\r\n@scenario:scenario\r\n@dialog:001[名[称]]\r\n\r\n  内容：12:30…\r\n\r\n\\@正文\r\n#注释\r\n\\#正文\r\n\\\\正文\r\n\r\n@banner:005  \r\n横幅翻译\r\n");
        var story = new Story(CreateScript(), data);
        Assert.Equal("横幅翻译", story.Events[0].FinalContent);
        var dialog = Assert.IsType<SekaiToolsBase.Story.StoryEvent.DialogStoryEvent>(story.Events[1]);
        Assert.Equal("名[称]", dialog.FinalCharacter);
        Assert.Equal("  内容：12:30…\n\n@正文\n#正文\n\\正文", dialog.FinalContent);
    }

    [Fact]
    public void ExportRoundTripsEscapesAndTrimsDialogue()
    {
        Translation[] values = [new EffectTranslate("地点"), new DialogTranslate("角色", "") { Body = " \n@内容\n#内容\n\\内容\n  " }];
        var text = TranslationFormatV2.Export(CreateScript(), values);
        Assert.Contains("@banner:005", text);
        var story = new Story(CreateScript(), Load(text));
        Assert.Equal("@内容\n#内容\n\\内容", story.Events[1].FinalContent);
    }

    [Fact]
    public void EmptyTranslationAndSpeakerRemainExplicit()
    {
        var story = new Story(CreateScript(), Load("@version:2\n@scenario:scenario\n@dialog:001[]\n@banner:005\n"));
        Assert.All(story.Events, item => Assert.Equal("", item.FinalContent));
        var dialog = Assert.IsType<SekaiToolsBase.Story.StoryEvent.DialogStoryEvent>(story.Events[1]);
        Assert.Equal("", dialog.FinalCharacter);
        Assert.Equal("", ((SekaiToolsBase.Story.StoryEvent.DialogStoryEvent)dialog.Clone()).FinalCharacter);
    }

    [Theory]
    [InlineData("@version:3\n@scenario:scenario")]
    [InlineData("@scenario:scenario\n@dialog:001[x]")]
    [InlineData("@version:2\n@scenario:scenario\n@dialog:001")]
    [InlineData("@version:2\n@scenario:scenario\n@dialog:001[x]\n@dialog:1[y]")]
    [InlineData("@version:2\n@scenario:scenario\n@end")]
    public void MalformedFilesFailWithFormatError(string text) => Assert.Throws<FormatException>(() => Load(text));

    [Theory]
    [InlineData("@version:2\n@scenario:other\n@dialog:001[x]\n@banner:005")]
    [InlineData("@version:2\n@scenario:scenario\n@dialog:001[x]")]
    [InlineData("@version:2\n@scenario:scenario\n@dialog:001[x]\n@marker:005")]
    public void IncompatibleFilesFailBeforeApplying(string text) =>
        Assert.Throws<FormatException>(() => new Story(CreateScript(), Load(text)));

    [Fact]
    public void JsonLoadingRetainsScenarioAndUnfilteredEffectIndex()
    {
        var source = CreateScript();
        source.SpecialEffectData = [new SpecialEffect(6, "", "", 0, 0), new SpecialEffect(8, "地点", "", 0, 0)];
        source.Snippets = [new Snippet { Action = 6 }, new Snippet { Action = 6 }, new Snippet { Action = 1 }];
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(source));
            var loaded = new Script(path);
            Assert.Equal("scenario", loaded.ScenarioId);
            Assert.Equal(new[] { 1 }, loaded.OriginalEffectIndices);
            Assert.Equal(("banner", 2), TranslationFormatV2.Keys(loaded)[0]);
        }
        finally { File.Delete(path); }
    }
}

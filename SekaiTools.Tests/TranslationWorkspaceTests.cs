using SekaiToolsBase.GameScript;
using SekaiToolsGUI.ViewModel.Translate;

namespace SekaiTools.Tests;

public class TranslationWorkspaceTests
{
    private static TranslatePageModel CreateModel()
    {
        var model = new TranslatePageModel();
        model.LoadScript(new GameScript
        {
            ScenarioId = "workspace",
            TalkData = [new("角色", "原文一", 0), new("角色", "原文二", 1)],
            Snippets = [new() { Action = 1 }, new() { Action = 1 }]
        }, "script.json");
        return model;
    }

    [Fact]
    public void DraftStatusDoesNotClaimThatFileWasSaved()
    {
        var model = CreateModel();
        Assert.Equal("尚未保存翻译文件", model.SaveStatus);
        ((LineDialogModel)model.Events[0]).Content.Translated = "译文。";
        model.MarkDraftResult(true);
        Assert.True(model.IsDirty);
        Assert.Contains("尚未写入翻译文件", model.SaveStatus);
        model.MarkDraftResult(false);
        Assert.Contains("草稿更新失败", model.SaveStatus);
        model.MarkSaved(path: "translation.txt");
        Assert.False(model.IsDirty);
        Assert.Contains("文件已保存", model.SaveStatus);
        model.Undo();
        Assert.Contains("有未保存修改", model.SaveStatus);
        model.Redo();
        Assert.Contains("文件已保存", model.SaveStatus);
    }
}

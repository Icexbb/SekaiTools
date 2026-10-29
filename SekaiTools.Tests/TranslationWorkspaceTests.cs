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

    [Fact]
    public void BlankDispositionIsUndoableAndFiltersDoNotChangeExportOrder()
    {
        var model = CreateModel();
        var line = model.Events[0];
        var untranslated = model.Export();
        Assert.Equal(2, model.UntranslatedCount);
        line.RequestAction("blank");
        Assert.Equal(1, model.UntranslatedCount);
        Assert.Equal(1, model.ExplicitEmptyCount);
        Assert.True(model.IsDirty);
        Assert.True(line.IsExplicitEmpty);
        Assert.Contains("有意留空", line.StateText);
        model.FilterIndex = 1;
        Assert.Single(model.VisibleEvents);
        Assert.Same(model.Events[1], model.VisibleEvents[0]);
        var exported = model.Export();
        model.SearchText = "不存在";
        Assert.Empty(model.VisibleEvents);
        Assert.Equal(exported, model.Export());
        Assert.Same(model.Events[1], model.FindNext(line, false));
        model.Undo();
        Assert.Equal(2, model.UntranslatedCount);
        Assert.Equal(0, model.ExplicitEmptyCount);
        Assert.Equal(untranslated, model.Export());
        model.Redo();
        Assert.True(line.IsExplicitEmpty);
    }

    [Fact]
    public void SearchAndNavigationUseStableScriptNumbers()
    {
        var model = CreateModel();
        model.SearchText = "原文二";
        Assert.Single(model.VisibleEvents);
        Assert.Equal(2, model.VisibleEvents[0].SequenceNumber);
        ((LineDialogModel)model.Events[0]).Content.Translated = "测试";
        model.FilterIndex = 2;
        model.SearchText = "测试";
        Assert.Single(model.VisibleEvents);
        Assert.Same(model.Events[0], model.FindNext(model.Events[1], true));
        model.FilterIndex = 0;
        model.SearchText = string.Empty;
        model.Events[1].RequestAction("original");
        Assert.Contains("沿用原文", model.Events[1].StateText);
        Assert.Equal(0, model.UntranslatedCount);
    }

    [Fact]
    public void PreflightReflectsWholeDocumentAndSelectedFormat()
    {
        var model = CreateModel();
        model.Events[0].RequestAction("blank");
        model.SearchText = "找不到";
        Assert.Contains("1 条未译正文将沿用原文", model.GetExportSummary(true));
        Assert.Contains("1 条正文已标为留空", model.GetExportSummary(true));
        Assert.Contains("旧 TXT 无法可靠保留", model.GetExportSummary(false));
        Assert.Equal(2, model.Events.Length);
        model.Events[1].RequestAction("original");
        Assert.DoesNotContain("条未译正文将沿用原文", model.GetExportSummary(true));
    }
}

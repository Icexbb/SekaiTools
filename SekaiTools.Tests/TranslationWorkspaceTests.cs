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
            TalkData = [new Talk("角色", "原文一", 0), new Talk("角色", "原文二", 1)],
            Snippets = [new Snippet { Action = 1 }, new Snippet { Action = 1 }]
        }, "script.json");
        return model;
    }

    [Theory]
    [InlineData(null, "新译文", false)]
    [InlineData("", "新增", true)]
    [InlineData("删除", "", true)]
    [InlineData("相同", "相同", false)]
    [InlineData("一\r\n二", "一\n二", false)]
    [InlineData("最初文本", "修改文本", true)]
    public void LoadTimeComparisonAppearsOnlyForActualLoadedTextChanges(string? loaded, string current, bool expected)
    {
        var item = new TranslateItemModel { LoadedText = loaded, Translated = current };
        Assert.Equal(expected, item.HasLoadedChanges);
        item.Translated = loaded ?? string.Empty;
        Assert.False(item.HasLoadedChanges);
    }

    [Fact]
    public void DraftWithoutLoadedFileDoesNotInventLoadTimeBaseline()
    {
        var model = CreateModel();
        ((LineDialogModel)model.Events[0]).Content.Translated = "新建译文。";
        model.MarkSaved(path: "new.txt");
        var restored = new TranslatePageModel();
        restored.RestoreDraft(model.DraftScript!, model.ScriptPath, model.TranslationPath,
            model.ReadValues(), model.SavedValues, loadedTranslationValues: []);
        Assert.False(restored.HasLoadedTranslation);
        Assert.Null(restored.LoadedTranslationValues);
    }

    [Fact]
    public void LoadedTranslationBaselineSurvivesSavingAndReferenceChangesButResetsOnReload()
    {
        var model = CreateModel();
        Assert.False(model.HasLoadedTranslation);
        var translation = new SekaiToolsBase.Story.Translation.TranslationData(null);
        translation.Translations.AddRange([
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "载入文本。"),
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "第二句。")]);
        model.LoadTranslation(translation, "loaded.txt");
        var line = (LineDialogModel)model.Events[0];
        Assert.Equal("载入文本。", line.Content.LoadedText);
        var baseline = model.LoadedTranslationValues!;
        line.Content.Translated = "修改文本。";
        model.MarkSaved();
        Assert.Equal(baseline, model.LoadedTranslationValues);
        Assert.False(model.IsDirty);
        model.LoadReferenceTranslation(translation, "reference.txt");
        model.ClearReference();
        Assert.Equal(baseline, model.LoadedTranslationValues);
        model.Undo();
        Assert.Equal(line.Content.LoadedText, line.Content.Translated);
        model.Redo();
        Assert.Equal(baseline, model.LoadedTranslationValues);
        translation.Translations[0] = new SekaiToolsBase.Story.Translation.DialogTranslate("新角色", "重新载入。");
        model.LoadTranslation(translation, "replacement.txt");
        Assert.Equal("重新载入。", ((LineDialogModel)model.Events[0]).Content.LoadedText);
        Assert.Equal("新角色", ((LineDialogModel)model.Events[0]).Character.LoadedText);
        model.ClearTranslation();
        Assert.False(model.HasLoadedTranslation);
        Assert.Null(((LineDialogModel)model.Events[0]).Content.LoadedText);
    }

    [Fact]
    public void DraftRestoreKeepsOriginalLoadedBaselineIndependentlyOfLastSave()
    {
        var model = CreateModel();
        var translation = new SekaiToolsBase.Story.Translation.TranslationData(null);
        translation.Translations.AddRange([
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "载入文本。"),
            new SekaiToolsBase.Story.Translation.DialogTranslate("角色", "第二句。")]);
        model.LoadTranslation(translation, "loaded.txt");
        var line = (LineDialogModel)model.Events[0];
        line.Content.Translated = "第一次修改。";
        model.MarkSaved();
        line.Content.Translated = "第二次修改。";
        var restored = new TranslatePageModel();
        restored.RestoreDraft(model.DraftScript!, model.ScriptPath, model.TranslationPath,
            model.ReadValues(), model.SavedValues, loadedTranslationValues: model.LoadedTranslationValues);
        Assert.Equal("载入文本。", ((LineDialogModel)restored.Events[0]).Content.LoadedText);
        Assert.Equal("第二次修改。", ((LineDialogModel)restored.Events[0]).Content.Translated);
        Assert.True(restored.IsDirty);
        var legacy = new TranslatePageModel();
        legacy.RestoreDraft(model.DraftScript!, model.ScriptPath, model.TranslationPath, model.ReadValues(), model.SavedValues);
        Assert.Equal("第一次修改。", ((LineDialogModel)legacy.Events[0]).Content.LoadedText);
        Assert.Throws<System.IO.InvalidDataException>(() => restored.RestoreDraft(model.DraftScript!, model.ScriptPath,
            model.TranslationPath, model.ReadValues(), model.SavedValues, loadedTranslationValues: ["bad"]));
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
    public void EditingRefreshesFiltersAndSearchImmediatelyIncludingUndo()
    {
        var model = CreateModel();
        var first = (LineDialogModel)model.Events[0];
        model.FilterIndex = 1;
        first.Content.Translated = "译文。";
        Assert.Single(model.VisibleEvents);
        Assert.DoesNotContain(first, model.VisibleEvents);
        model.Undo();
        Assert.Equal(2, model.VisibleEvents.Length);
        model.Redo();
        Assert.Single(model.VisibleEvents);
        model.FilterIndex = 0;
        model.SearchText = "新的译名";
        Assert.Empty(model.VisibleEvents);
        first.Character.Translated = "新的译名";
        Assert.Equal(2, model.VisibleEvents.Length);
        model.Undo();
        Assert.Empty(model.VisibleEvents);
        model.SearchText = "译文";
        Assert.Single(model.VisibleEvents);
        first.Content.Translated = "修改后。";
        Assert.Empty(model.VisibleEvents);
    }

    [Fact]
    public void TypeFilterCombinesWithStatusAndSearchWithoutChangingExport()
    {
        var model = new TranslatePageModel();
        model.LoadScript(new GameScript
        {
            ScenarioId = "mixed",
            TalkData = [new Talk("角色", "教室对话", 0)],
            SpecialEffectData = [new SpecialEffect(8, "教室", "", 0, 0)],
            Snippets = [new Snippet { Action = 1 }, new Snippet { Action = 6 }]
        }, "mixed.json");
        var exported = model.Export();
        model.TypeFilterIndex = 1;
        Assert.IsType<LineDialogModel>(Assert.Single(model.VisibleEvents));
        model.TypeFilterIndex = 2;
        var place = Assert.IsType<LineEffectModel>(Assert.Single(model.VisibleEvents));
        model.FilterIndex = 1;
        model.SearchText = "教室";
        Assert.Single(model.VisibleEvents);
        place.Content.Translated = "教室译文";
        Assert.Empty(model.VisibleEvents);
        model.Undo();
        Assert.Single(model.VisibleEvents);
        Assert.Equal(exported, model.Export());
        model.Events[0].RequestAction("context");
        Assert.Equal(0, model.TypeFilterIndex);
        Assert.Equal(2, model.VisibleEvents.Length);
        model.TypeFilterIndex = 2;
        Assert.Same(model.Events[0], model.FindNext(place, false));
        Assert.Equal(0, model.TypeFilterIndex);
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

    [Fact]
    public void AdoptingReferenceChangesOnlyBodyAndKeepsExplicitEmptyUndoable()
    {
        var model = CreateModel();
        var line = (LineDialogModel)model.Events[0];
        line.Content.ReferenceAvailable = true;
        line.Content.ReferencePreserveEmpty = true;
        line.Content.Reference = string.Empty;
        line.RequestAction("reference");
        Assert.True(line.IsExplicitEmpty);
        Assert.True(model.Events[1].IsUntranslated);
        Assert.Equal("角色", line.Character.Translated);
        model.Undo();
        Assert.True(line.IsUntranslated);
        model.ShowReference = false;
        Assert.All(model.Events, item => Assert.False(item.ShowReference));
    }

    [Fact]
    public void EffectSynchronizationCanBeRestrictedToCurrentEntry()
    {
        var model = new TranslatePageModel();
        model.LoadScript(new GameScript
        {
            ScenarioId = "effects",
            SpecialEffectData = [new SpecialEffect(8, "教室", "", 0, 0), new SpecialEffect(8, "教室", "", 0, 0)],
            Snippets = [new Snippet { Action = 6 }, new Snippet { Action = 6 }]
        }, "effects.json");
        var first = (LineEffectModel)model.Events[0];
        var second = (LineEffectModel)model.Events[1];
        Assert.Equal(2, first.MatchingCount);
        model.SyncMatchingContent = false;
        first.Content.Translated = "地点一";
        Assert.Empty(second.Content.Translated);
        model.SyncMatchingContent = true;
        first.Content.Translated = "地点二";
        Assert.Equal("地点二", second.Content.Translated);
        Assert.Contains("共 2 处", model.LastEditFeedback);
        model.Undo();
        Assert.Equal("地点一", first.Content.Translated);
        Assert.Empty(second.Content.Translated);
    }

    [Fact]
    public void ProjectSpeakerSwitchAppliesToHiddenRowsAndUndoIsAtomic()
    {
        var model = CreateModel();
        var first = (LineDialogModel)model.Events[0];
        var second = (LineDialogModel)model.Events[1];
        model.SearchText = "原文一";
        Assert.Single(model.VisibleEvents);
        model.SyncMatchingSpeakers = false;
        first.Character.Translated = "甲";
        Assert.Equal("角色", second.Character.Translated);
        model.SyncMatchingSpeakers = true;
        Assert.Equal("角色", second.Character.Translated);
        first.Character.Translated = "乙";
        Assert.Equal("乙", second.Character.Translated);
        Assert.Empty(second.Content.Translated);
        model.Undo();
        Assert.Equal("甲", first.Character.Translated);
        Assert.Equal("角色", second.Character.Translated);
        model.Redo();
        Assert.Equal("乙", second.Character.Translated);
    }

    [Fact]
    public void DraftRestoresProjectSynchronizationOptions()
    {
        var model = CreateModel();
        var restored = new TranslatePageModel();
        restored.RestoreDraft(model.DraftScript!, model.ScriptPath, "", model.ReadValues(), model.SavedValues,
            syncMatchingContent: false, syncMatchingSpeakers: false);
        Assert.False(restored.SyncMatchingContent);
        Assert.False(restored.SyncMatchingSpeakers);
        restored.LoadScript(model.DraftScript!, model.ScriptPath);
        Assert.True(restored.SyncMatchingContent);
        Assert.True(restored.SyncMatchingSpeakers);
    }

    [Fact]
    public void ContinuousTypingGroupsButSaveAndPasteBoundariesRemainSeparate()
    {
        var model = CreateModel();
        var body = model.Events[0].Body;
        model.BeginTextEdit(body);
        body.Translated = "一";
        body.Translated = "一二";
        model.BreakEditGroup(); // a paste is a distinct operation
        body.Translated = "一二粘贴。";
        model.Undo();
        Assert.Equal("一二", body.Translated);
        model.Undo();
        Assert.Empty(body.Translated);
        model.Redo();
        model.MarkSaved();
        body.Translated = "保存后的输入。";
        model.Undo();
        Assert.Equal("一二", body.Translated);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void DeferredFormattingAndUndoPreserveExactInput()
    {
        var model = CreateModel();
        var line = (LineDialogModel)model.Events[0];
        line.DeferFormatting = true;
        line.Content.Translated = "欸,你好!";
        Assert.Equal("欸,你好!", line.Content.Translated);
        line.CompleteTextEdit();
        Assert.Equal("诶，你好！", line.Content.Translated);
        model.Undo();
        Assert.Equal("欸,你好!", line.Content.Translated);
        model.AutoFormat = false;
        line.Content.Translated = "欸?";
        line.CompleteTextEdit();
        Assert.Equal("欸?", line.Content.Translated);
    }

    [Fact]
    public void ThreeLineValidationAccountsForSelectionAndAllowsRepairingImportedText()
    {
        var line = (LineDialogModel)CreateModel().Events[0];
        Assert.False(line.CanReplaceText("一\n二\n三", 5, 0, "\n四"));
        Assert.Contains("最多三行", line.InputHint);
        Assert.True(line.CanReplaceText("一\n二\n三", 0, 5, "替换\n正文"));
        Assert.True(line.CanReplaceText("一\n二\n三\n四", 0, 1, "修正"));
        Assert.False(line.CanReplaceText("一\n二\n三\n四", 7, 0, "\r\n五"));
    }

    [Fact]
    public void RestoringDraftRetainsExportFormatAndRawText()
    {
        var model = CreateModel();
        model.AutoFormat = false;
        model.Events[0].Body.Translated = "欸,原样!";
        var restored = new TranslatePageModel();
        restored.RestoreDraft(model.DraftScript!, "script.json", "legacy.txt", model.ReadValues(), model.SavedValues,
            false, false, 2);
        Assert.False(restored.ExportUsesV2);
        Assert.False(restored.AutoFormat);
        Assert.Equal(2, restored.ActiveSequenceNumber);
        Assert.Equal("欸,原样!", restored.Events[0].Body.Translated);
    }

    [Fact]
    public void ViewingContextClearsFilterWithoutChangingTranslation()
    {
        var model = CreateModel();
        model.SearchText = "原文二";
        var before = model.Export();
        LineModel? requested = null;
        model.NavigationRequested += (_, line) => requested = line;
        model.Events[1].RequestAction("context");
        Assert.Equal(2, model.VisibleEvents.Length);
        Assert.Same(model.Events[1], requested);
        Assert.Equal(before, model.Export());
        Assert.False(model.CanUndo);
    }
}
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class TranslatePageModel : ViewModelBase
{
    private GameScript? _loadedScript;
    private Story? _referenceStory;
    private IReadOnlyDictionary<string, FlashbackEventOrigin> _flashbackOrigins = new Dictionary<string, FlashbackEventOrigin>();

    public string[] FlashbackSources => Events.OfType<LineDialogModel>().Where(line => line.IsFlashback)
        .SelectMany(line => line.Flashback!.Sources.Where(source => source != line.Flashback.MainSource)).Distinct().ToArray();

    public void ApplyFlashbackOrigins(IReadOnlyDictionary<string, FlashbackEventOrigin> origins)
    {
        _flashbackOrigins = origins;
        foreach (var line in Events.OfType<LineDialogModel>()) line.ApplyFlashbackOrigins(origins);
    }

    public string ScriptPath
    {
        get => GetProperty(string.Empty);
        private set { SetProperty(value); OnPropertyChanged(nameof(HasScript)); }
    }

    public string TranslationPath
    {
        get => GetProperty(string.Empty);
        private set { SetProperty(value); OnPropertyChanged(nameof(HasTranslation)); }
    }

    public string ReferenceTranslationPath
    {
        get => GetProperty(string.Empty);
        private set { SetProperty(value); OnPropertyChanged(nameof(HasReferenceTranslation)); }
    }

    public bool HasScript => !string.IsNullOrEmpty(ScriptPath);
    public bool HasTranslation => !string.IsNullOrEmpty(TranslationPath);
    public bool HasReferenceTranslation => !string.IsNullOrEmpty(ReferenceTranslationPath);

    public bool IsDirty
    {
        get => GetProperty(false);
        private set { SetProperty(value); OnPropertyChanged(nameof(SaveStatus)); }
    }

    public string DraftStatus
    {
        get => GetProperty(string.Empty);
        private set { SetProperty(value); OnPropertyChanged(nameof(SaveStatus)); }
    }

    public string SaveStatus => !HasScript ? "请先载入剧本" : IsDirty
        ? "有未保存修改 · " + DraftStatus
        : HasTranslation ? "文件已保存 · " + System.IO.Path.GetFileName(TranslationPath)
        : "尚未保存翻译文件";

    public void MarkDraftPending() => DraftStatus = "等待更新恢复草稿";
    public void MarkDraftResult(bool success) => DraftStatus = success ? "恢复草稿已更新（尚未写入翻译文件）" : "草稿更新失败，请保存文件";

    public void MarkSaved(EditValue[]? values = null, string? path = null)
    {
        _savedValues = values ?? ReadValues();
        if (path != null) TranslationPath = path;
        IsDirty = !ReadValues().SequenceEqual(_savedValues);
        OnPropertyChanged(nameof(SaveStatus));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LoadScript(GameScript script, string path)
    {
        var story = new Story(script, new TranslationData(null));
        Clear();
        _loadedScript = script;
        Story = story;
        ScriptPath = path;
        MarkSaved();
    }

    public void LoadTranslation(TranslationData translation, string path)
    {
        var script = _loadedScript ?? throw new InvalidOperationException("请先载入剧本");
        Story = new Story(script, translation);
        if (_referenceStory != null)
            ApplyReference(_referenceStory);
        TranslationPath = path;
        MarkSaved();
    }

    public void LoadReferenceTranslation(TranslationData translation, string path)
    {
        var script = _loadedScript ?? throw new InvalidOperationException("请先载入剧本");
        var reference = new Story(script, translation);
        ApplyReference(reference);
        _referenceStory = reference;
        ReferenceTranslationPath = path;
    }

    public bool IsTranslationApplicable(TranslationData translation) =>
        _loadedScript != null && translation.IsApplicable(_loadedScript);

    public string? GetTranslationMismatchReason(TranslationData translation) =>
        _loadedScript == null ? "请先载入剧本文件。" : translation.GetMismatchReason(_loadedScript);

    public void ClearTranslation()
    {
        if (_loadedScript == null) return;
        Story = new Story(_loadedScript, new TranslationData(null));
        if (_referenceStory != null)
            ApplyReference(_referenceStory);
        TranslationPath = string.Empty;
        MarkSaved();
    }

    public bool IsEmpty => Story.Events.Length == 0;

    public Story Story
    {
        get => GetProperty(Story.Empty());
        set
        {
            ClearEventRegisters();
            SetProperty(value);
            var flashbacks = FlashbackAnalyzer.Analyze(_loadedScript?.TalkData ?? []);
            var events = new List<LineModel>();
            foreach (var baseStoryEvent in value.Events)
                switch (baseStoryEvent)
                {
                    case DialogStoryEvent dialogStoryEvent:
                        var lineDialogModel = new LineDialogModel(dialogStoryEvent,
                            (uint)dialogStoryEvent.Index < (uint)flashbacks.Length ? flashbacks[dialogStoryEvent.Index] : null);
                        lineDialogModel.ApplyFlashbackOrigins(_flashbackOrigins);
                        lineDialogModel.CharacterTranslateChanged += OnLineDialogModelOnCharacterTranslateChanged;
                        lineDialogModel.ContentTranslateChanged += OnDialogContentChanged;
                        events.Add(lineDialogModel);
                        break;
                    default:
                        var lineEffectModel = new LineEffectModel(baseStoryEvent);
                        lineEffectModel.ContentTranslateChanged += OnLineEffectModelOnContentTranslateChanged;
                        events.Add(lineEffectModel);
                        break;
                }

            for (var i = 0; i < events.Count; i++)
            {
                events[i].SequenceNumber = i + 1;
                events[i].ActionRequested += OnLineAction;
                events[i].ShowReference = ShowReference;
                events[i].MatchingCount = events[i] is LineDialogModel d
                    ? events.OfType<LineDialogModel>().Count(other => other.Character.Original == d.Character.Original)
                    : events.OfType<LineEffectModel>().Count(other => other.Content.Original == events[i].Body.Original);
            }
            Events = events.ToArray();
            ResetHistory();
            SearchText = string.Empty;
            FilterIndex = 0;
            RefreshVisibleEvents();
            NotifyProgress();
            LastEditFeedback = string.Empty;
        }
    }

    public LineModel[] Events
    {
        get => GetProperty(Array.Empty<LineModel>());
        set => SetProperty(value);
    }

    public string Result
    {
        get
        {
            var result = Events.Select(lineModel => lineModel.Result).ToList();

            return string.Join("\n", result);
        }
    }

    public string Export(bool useV2 = true)
    {
        if (!useV2) return Result;
        var values = Events.Select(line => line switch
        {
            LineDialogModel dialog => (Translation)new DialogTranslate(dialog.Character.Result, dialog.Content.Result.Trim()),
            LineEffectModel effect => new EffectTranslate(effect.Content.Result),
            _ => throw new InvalidOperationException("未知翻译条目")
        }).ToArray();
        return TranslationFormatV2.Export(_loadedScript ?? throw new InvalidOperationException("请先载入剧本"), values);
    }

    public void Clear()
    {
        _flashbackOrigins = new Dictionary<string, FlashbackEventOrigin>();
        Story = Story.Empty();
        _loadedScript = null;
        _referenceStory = null;
        ScriptPath = string.Empty;
        TranslationPath = string.Empty;
        ReferenceTranslationPath = string.Empty;
        MarkSaved();
    }

    private void ClearEventRegisters()
    {
        // 取消对现有 Events 上的订阅，避免内存泄漏 / 重复回调
        foreach (var lineModel in Events)
        {
            lineModel.ActionRequested -= OnLineAction;
            switch (lineModel)
            {
                case LineDialogModel lineDialogModel:
                    lineDialogModel.CharacterTranslateChanged -= OnLineDialogModelOnCharacterTranslateChanged;
                    lineDialogModel.ContentTranslateChanged -= OnDialogContentChanged;
                    break;
                case LineEffectModel lineEffectModel:
                    lineEffectModel.ContentTranslateChanged -= OnLineEffectModelOnContentTranslateChanged;
                    break;
            }
        }
    }

    private void OnLineDialogModelOnCharacterTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineDialogModel changedLine) return;
        if (_restoringHistory) return;
        changedLine.CharacterTranslateChangedEnabled = false; // 防止递归调用

        // 当角色名称翻译发生变化时，更新所有 LineDialogModel 的 Check
        foreach (var line in Events.OfType<LineDialogModel>())
        {
            if (line.Character.Original != changedLine.Character.Original) continue;
            if (line == changedLine) continue;
            line.CharacterTranslateChangedEnabled = false;
            line.Character.Translated = changedLine.Character.Translated;
            line.CharacterTranslateChangedEnabled = true;
        }

        changedLine.CharacterTranslateChangedEnabled = true; // 防止递归调用
        RecordEdit();
        LastEditFeedback = $"已同步角色译名，共 {changedLine.MatchingCount} 处；可使用撤销恢复。";
    }

    private void OnLineEffectModelOnContentTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineEffectModel changedLine) return;
        if (_restoringHistory) return;
        changedLine.ContentTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineEffectModel>())
        {
            if (!changedLine.SyncMatchingContent) break;
            if (line.Content.Original != changedLine.Content.Original) continue;
            if (line == changedLine) continue;
            line.ContentTranslateChangedEnabled = false;
            line.Content.Translated = changedLine.Content.Translated;
            line.ContentTranslateChangedEnabled = true;
        }

        changedLine.ContentTranslateChangedEnabled = true;
        RecordEdit();
        LastEditFeedback = changedLine.SyncMatchingContent
            ? $"已同步相同场景文字，共 {changedLine.MatchingCount} 处；可使用撤销恢复。"
            : "已修改当前场景文字；其他同文条目未改动。";
    }

    public void ApplyReference(Story story)
    {
        for (var i = 0; i < story.Events.Length; i++)
        {
            var line = Events[i];
            var ev = story.Events[i];
            switch (line)
            {
                case LineDialogModel lineDialogModel when ev is DialogStoryEvent dialogStoryEvent:
                    {
                        if (lineDialogModel.Character.Original != dialogStoryEvent.CharacterOriginal
                            || lineDialogModel.Content.Original != dialogStoryEvent.BodyOriginal) continue;
                        lineDialogModel.Character.Reference = dialogStoryEvent.CharacterTranslated;
                        lineDialogModel.Content.Reference = dialogStoryEvent.BodyTranslated;
                        lineDialogModel.Content.ReferenceAvailable = true;
                        lineDialogModel.Content.ReferencePreserveEmpty = dialogStoryEvent.HasExplicitTranslation;
                        break;
                    }
                case LineEffectModel lineEffectModel:
                    {
                        if (lineEffectModel.Content.Original != ev.BodyOriginal) continue;
                        lineEffectModel.Content.Reference = ev.BodyTranslated;
                        lineEffectModel.Content.ReferenceAvailable = true;
                        lineEffectModel.Content.ReferencePreserveEmpty = ev.HasExplicitTranslation;

                        break;
                    }
            }
        }
    }

    public void ClearReference()
    {
        _referenceStory = null;
        ReferenceTranslationPath = string.Empty;
        foreach (var line in Events)
        {
            line.Body.ReferenceAvailable = false;
            switch (line)
            {
                case LineDialogModel lineDialogModel:
                    lineDialogModel.Character.Reference = string.Empty;
                    lineDialogModel.Content.Reference = string.Empty;
                    break;
                case LineEffectModel lineEffectModel:
                    lineEffectModel.Content.Reference = string.Empty;
                    break;
            }
        }
    }
}

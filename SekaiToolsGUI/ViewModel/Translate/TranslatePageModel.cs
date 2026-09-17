using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsGUI.ViewModel.Translate;

public class TranslatePageModel : ViewModelBase
{
    private GameScript? _loadedScript;
    private Story? _referenceStory;

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
        private set => SetProperty(value);
    }

    public void MarkSaved() => IsDirty = false;

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
            var events = new List<LineModel>();
            foreach (var baseStoryEvent in value.Events)
                switch (baseStoryEvent)
                {
                    case DialogStoryEvent dialogStoryEvent:
                        var lineDialogModel = new LineDialogModel(dialogStoryEvent);
                        lineDialogModel.CharacterTranslateChanged += OnLineDialogModelOnCharacterTranslateChanged;
                        events.Add(lineDialogModel);
                        break;
                    default:
                        var lineEffectModel = new LineEffectModel(baseStoryEvent);
                        lineEffectModel.ContentTranslateChanged += OnLineEffectModelOnContentTranslateChanged;
                        events.Add(lineEffectModel);
                        break;
                }

            Events = events.ToArray();
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

    public void Clear()
    {
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
            switch (lineModel)
            {
                case LineDialogModel lineDialogModel:
                    lineDialogModel.CharacterTranslateChanged -= OnLineDialogModelOnCharacterTranslateChanged;
                    break;
                case LineEffectModel lineEffectModel:
                    lineEffectModel.ContentTranslateChanged -= OnLineEffectModelOnContentTranslateChanged;
                    break;
            }
    }

    private void OnLineDialogModelOnCharacterTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineDialogModel changedLine) return;
        IsDirty = true;
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
    }

    private void OnLineEffectModelOnContentTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineEffectModel changedLine) return;
        IsDirty = true;
        changedLine.ContentTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineEffectModel>())
        {
            if (line.Content.Original != changedLine.Content.Original) continue;
            if (line == changedLine) continue;
            line.ContentTranslateChangedEnabled = false;
            line.Content.Translated = changedLine.Content.Translated;
            line.ContentTranslateChangedEnabled = true;
        }

        changedLine.ContentTranslateChangedEnabled = true;
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
                    break;
                }
                case LineEffectModel lineEffectModel:
                {
                    if (lineEffectModel.Content.Original != ev.BodyOriginal) continue;
                    lineEffectModel.Content.Reference = ev.BodyTranslated;

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

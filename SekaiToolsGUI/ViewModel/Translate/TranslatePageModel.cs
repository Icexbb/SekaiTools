using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsGUI.ViewModel.Translate;

public class TranslatePageModel : ViewModelBase
{
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
                    if (lineEffectModel.Content.Original == ev.BodyOriginal) continue;
                    lineEffectModel.Content.Reference = ev.BodyTranslated;

                    break;
                }
            }
        }
    }
}
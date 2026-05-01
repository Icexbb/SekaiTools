using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsMauiText.ViewModel;

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
                        lineDialogModel.CharacterTranslateChanged += OnCharacterTranslateChanged;
                        lineDialogModel.ContentTranslateChanged += OnDialogContentTranslateChanged;
                        events.Add(lineDialogModel);
                        break;
                    default:
                        var lineEffectModel = new LineEffectModel(baseStoryEvent);
                        lineEffectModel.ContentTranslateChanged += OnEffectContentTranslateChanged;
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
            var result = Events.Select(m => m.Result).ToList();
            return string.Join("\n", result);
        }
    }

    public void Clear()
    {
        Story = Story.Empty();
    }

    private void ClearEventRegisters()
    {
        foreach (var lineModel in Events)
            switch (lineModel)
            {
                case LineDialogModel dialogModel:
                    dialogModel.CharacterTranslateChanged -= OnCharacterTranslateChanged;
                    dialogModel.ContentTranslateChanged -= OnDialogContentTranslateChanged;
                    break;
                case LineEffectModel effectModel:
                    effectModel.ContentTranslateChanged -= OnEffectContentTranslateChanged;
                    break;
            }
    }

    private void OnCharacterTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineDialogModel changedLine) return;
        changedLine.CharacterTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineDialogModel>())
        {
            if (line.OriginalCharacter != changedLine.OriginalCharacter) continue;
            if (line == changedLine) continue;
            line.CharacterTranslateChangedEnabled = false;
            line.TranslatedCharacter = changedLine.TranslatedCharacter;
            line.CharacterTranslateChangedEnabled = true;
        }

        changedLine.CharacterTranslateChangedEnabled = true;
    }

    private void OnDialogContentTranslateChanged(object? sender, EventArgs args)
    {
        // Dialog content changes are per-line; no auto-sync needed
    }

    private void OnEffectContentTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineEffectModel changedLine) return;
        changedLine.ContentTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineEffectModel>())
        {
            if (line.OriginalContent != changedLine.OriginalContent) continue;
            if (line == changedLine) continue;
            line.ContentTranslateChangedEnabled = false;
            line.TranslatedContent = changedLine.TranslatedContent;
            line.ContentTranslateChangedEnabled = true;
        }

        changedLine.ContentTranslateChangedEnabled = true;
    }

    public void ApplyReference(Story story)
    {
        for (var i = 0; i < story.Events.Length; i++)
        {
            if (i >= Events.Length) break;
            var line = Events[i];
            var ev = story.Events[i];
            switch (line)
            {
                case LineDialogModel lineDialogModel when ev is DialogStoryEvent dialogStoryEvent:
                    if (lineDialogModel.OriginalCharacter != dialogStoryEvent.CharacterOriginal
                        || lineDialogModel.OriginalContent != dialogStoryEvent.BodyOriginal) continue;
                    lineDialogModel.CharacterReference = dialogStoryEvent.CharacterTranslated;
                    lineDialogModel.ContentReference = dialogStoryEvent.BodyTranslated;
                    break;
                case LineEffectModel lineEffectModel:
                    if (lineEffectModel.OriginalContent != ev.BodyOriginal) continue;
                    lineEffectModel.ContentReference = ev.BodyTranslated;
                    break;
            }
        }
    }
}
using System.ComponentModel;
using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsAvalonia.ViewModel.Translate;

public class LineEffectModel : LineModel
{
    public LineEffectModel(BaseStoryEvent eBaseStoryEvent)
    {
        Content.Original = eBaseStoryEvent.BodyOriginal;
        Content.Translated = eBaseStoryEvent.BodyTranslated;
        Content.PropertyChanged += OnContentPropertyChanged;
    }

    public TranslateItemModel Content { get => GetProperty(new TranslateItemModel()); set => SetProperty(value); }
    public override string Result => Content.Result;
    public bool ContentTranslateChangedEnabled { get; set; } = true;

    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TranslateItemModel.Translated)) return;
        if (ContentTranslateChangedEnabled) ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ContentTranslateChanged;
}

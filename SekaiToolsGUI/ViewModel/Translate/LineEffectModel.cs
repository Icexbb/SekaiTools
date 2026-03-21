using System.ComponentModel;
using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsGUI.ViewModel.Translate;

public class LineEffectModel : LineModel
{
    public LineEffectModel(BaseStoryEvent eBaseStoryEvent)
    {
        Content.Original = eBaseStoryEvent.BodyOriginal;
        Content.Translated = eBaseStoryEvent.BodyTranslated;
        Content.PropertyChanged += OnContentPropertyChanged;
    }

    public TranslateItemModel Content
    {
        get => GetProperty(new TranslateItemModel());
        set => SetProperty(value);
    }

    public override string Result => Content.Result;
    public bool ContentTranslateChangedEnabled { get; set; } = true;

    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 只有当 Translated 变化时才触发
        if (e.PropertyName != nameof(TranslateItemModel.Translated)) return;
        // 更新父级的 Check, LineCount 等
        if (ContentTranslateChangedEnabled) ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ContentTranslateChanged;
}
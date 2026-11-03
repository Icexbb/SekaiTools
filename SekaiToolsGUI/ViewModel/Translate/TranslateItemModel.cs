using SekaiToolsBase.Story.Translation;

namespace SekaiToolsGUI.ViewModel.Translate;

public class TranslateItemModel : ViewModelBase
{
    public string? LoadedText
    {
        get => GetProperty<string?>(null);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasLoadedChanges));
        }
    }

    public bool HasLoadedChanges => LoadedText is { } loaded &&
                                    TextDifference.Compare(Translated, loaded).Current.Count > 0;

    public string Original
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string Reference
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public bool ReferenceAvailable
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool ReferencePreserveEmpty { get; set; }

    public string Translated
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasLoadedChanges));
        }
    }

    public bool PreserveEmpty
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string Result => !PreserveEmpty && string.IsNullOrWhiteSpace(Translated) ? Original : Translated;
}

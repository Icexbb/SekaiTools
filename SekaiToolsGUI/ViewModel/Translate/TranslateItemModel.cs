namespace SekaiToolsGUI.ViewModel.Translate;

public class TranslateItemModel : ViewModelBase
{
    public string? LoadedText
    {
        get => GetProperty<string?>(null);
        set => SetProperty(value);
    }

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
        set => SetProperty(value);
    }

    public bool PreserveEmpty
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string Result => !PreserveEmpty && string.IsNullOrWhiteSpace(Translated) ? Original : Translated;
}

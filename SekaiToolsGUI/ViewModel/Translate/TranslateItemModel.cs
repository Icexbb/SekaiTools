namespace SekaiToolsGUI.ViewModel.Translate;

public class TranslateItemModel : ViewModelBase
{
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

    public string Translated
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string Result => string.IsNullOrWhiteSpace(Translated) ? Original : Translated;
}
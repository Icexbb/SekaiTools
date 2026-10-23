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

    public bool PreserveEmpty { get; set; }
    public string Result => !PreserveEmpty && string.IsNullOrWhiteSpace(Translated) ? Original : Translated;
}

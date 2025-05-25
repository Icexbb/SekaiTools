namespace SekaiToolsGUI.ViewModel;

public class DownloadPageModel : ViewModelBase
{
    public static DownloadPageModel Instance { get; } = new();

    public int CurrentSourceIndex
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public DownloadSourceEditorModel CurrentSource => SourceData[CurrentSourceIndex];

    public DownloadSourceEditorModel[] SourceData
    {
        get => GetProperty(Array.Empty<DownloadSourceEditorModel>());
        set => SetProperty(value);
    }
}
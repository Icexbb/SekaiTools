using SekaiDataFetch.Source;

namespace SekaiToolsGUI.ViewModel;

public class DownloadPageModel : ViewModelBase
{
    public static DownloadPageModel Instance { get; } = new();

    public int CurrentSourceIndex
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public SourceData CurrentSource => SourceData[CurrentSourceIndex];

    public SourceData[] SourceData
    {
        get => GetProperty(Array.Empty<SourceData>());
        set => SetProperty(value);
    }
}
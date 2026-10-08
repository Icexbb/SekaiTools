using SekaiDataFetch.Source;

namespace SekaiToolsGUI.ViewModel.Download;

public class DownloadPageModel : ViewModelBase
{
    private readonly SourceData[] _defaultSources = SekaiDataFetch.Source.SourceData.Default;
    public static DownloadPageModel Instance { get; } = new();

    public int CurrentSourceIndex
    {
        get => GetProperty(0);
        set
        {
            // A ComboBox can report -1 while its ItemsSource is being replaced.
            SetProperty(value >= 0 && value < SourceData.Length ? value : 0);
            OnPropertyChanged(nameof(CurrentSource));
        }
    }

    public SourceData CurrentSource
    {
        get
        {
            var sources = SourceData;
            var index = CurrentSourceIndex;
            return sources[index >= 0 && index < sources.Length ? index : 0];
        }
    }

    public SourceData[] SourceData
    {
        get => GetProperty(_defaultSources);
        set
        {
            SetProperty(value is { Length: > 0 } ? value : _defaultSources);
            CurrentSourceIndex = 0;
            // Reapply the first selection even when the model index was already zero.
            OnPropertyChanged(nameof(CurrentSourceIndex));
        }
    }
}

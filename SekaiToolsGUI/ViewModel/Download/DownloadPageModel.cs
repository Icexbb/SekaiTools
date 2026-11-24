using System.IO;
using System.Text.Json;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsBase;
using SekaiToolsConfiguration;

namespace SekaiToolsGUI.ViewModel.Download;

public class DownloadPageModel : ViewModelBase
{
    private readonly SourceData[] _defaultSources = SekaiDataFetch.Source.SourceData.Default;
    private Task? _discovery;
    private Task<SourceData[]>? _ranking;
    private bool _automatic;
    public SourceData[]? RankedSources { get; private set; }
    public Exception? DiscoveryError { get; private set; }
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
            if (RankedSources is { Length: > 0 }) return RankedSources[0];
            var index = CurrentSourceIndex;
            return sources[index >= 0 && index < sources.Length ? index : 0];
        }
    }

    public IReadOnlyList<SourceData> OrderedSources => RankedSources ??
        new[] { CurrentSource }.Concat(SourceData.Where(source => source != CurrentSource)).ToArray();

    public async Task UpdateSourcesAsync(bool automatic, Proxy proxy)
    {
        _automatic = automatic;
        await (_discovery ??= DiscoverAsync(proxy));
        if (_automatic)
        {
            var ranked = await (_ranking ??= RankAsync(proxy));
            if (_automatic)
                RankedSources = ranked;
        }
        else
        {
            RankedSources = null;
            _ranking = null;
        }
    }

    private async Task DiscoverAsync(Proxy proxy)
    {
        using var fetcher = new Fetcher();
        fetcher.SetProxy(proxy);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            var json = await fetcher.Fetch(NetworkEndpoints.Current.Resources.SourceListUrl, timeout.Token);
            SourceData = JsonSerializer.Deserialize<SourceData[]>(json) is { Length: > 0 } sources
                ? sources
                : throw new InvalidDataException("数据源列表为空。");
        }
        catch (Exception error)
        {
            DiscoveryError = error;
            SourceData = _defaultSources;
        }
    }

    private async Task<SourceData[]> RankAsync(Proxy proxy)
    {
        using var fetcher = new Fetcher();
        fetcher.SetProxy(proxy);
        return await DownloadSources.RankAsync(fetcher, SourceData);
    }

    public SourceData[] SourceData
    {
        get => GetProperty(_defaultSources);
        set
        {
            RankedSources = null;
            _ranking = null;
            SetProperty(value is { Length: > 0 } ? value : _defaultSources);
            CurrentSourceIndex = 0;
            // Reapply the first selection even when the model index was already zero.
            OnPropertyChanged(nameof(CurrentSourceIndex));
        }
    }
}

using SekaiDataFetch;

namespace SekaiToolsGUI.Interface;

public interface IDownloadStoryBrowser
{
    IReadOnlyList<DownloadCandidate> VisibleDownloads { get; }
    event EventHandler? ResultsChanged;
    void ApplySearch(string query);
    void ResetFilters();
}

using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Special;

public partial class SpecialStoryTab : IDownloadStoryBrowser
{
    private string _query = "";

    private IEnumerable<DownloadCandidate> FilteredCandidates =>
        SpecialStoryTypeSelector.SelectedItem is string selected &&
        ListSpecialStory.Data.TryGetValue(selected, out var story)
            ? StoryDownloadCandidates.Special(story, DownloadPageModel.Instance.CurrentSource)
            : Enumerable.Empty<DownloadCandidate>();

    public event EventHandler? ResultsChanged;

    public IReadOnlyList<DownloadCandidate> VisibleDownloads =>
        FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();

    public void ApplySearch(string query)
    {
        _query = query;
        RefreshSelection();
    }

    public void ResetFilters()
    {
        SpecialStoryTypeSelector.SelectedIndex = SpecialStoryTypeSelector.Items.Count > 0 ? 0 : -1;
        RefreshSelection();
    }
}
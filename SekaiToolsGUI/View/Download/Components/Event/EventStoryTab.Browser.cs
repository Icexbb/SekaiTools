using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryTab : IDownloadStoryBrowser
{
    private string _query = "";
    public event EventHandler? ResultsChanged;
    private IEnumerable<DownloadCandidate> FilteredCandidates => ViewModel.EventStories.SelectMany(story => StoryDownloadCandidates.Event(story, DownloadPageModel.Instance.CurrentSource));
    public IReadOnlyList<DownloadCandidate> VisibleDownloads => FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();
    public void ApplySearch(string query) { _query = query; RefreshItems(); }
    public void ResetFilters() { CharacterComboBox.SelectedIndex = 0; BoxType.SelectedIndex = 0; RefreshItems(); }
}

using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Action;

public partial class ActionStoryTab : IDownloadStoryBrowser
{
    private string _query = "";
    public event EventHandler? ResultsChanged;
    private IEnumerable<DownloadCandidate> FilteredCandidates => ViewModel.EventStories.Select(story => StoryDownloadCandidates.Action(story, DownloadPageModel.Instance.CurrentSource));
    public IReadOnlyList<DownloadCandidate> VisibleDownloads => FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();
    public void ApplySearch(string query) { _query = query; RefreshItems(); }
    public void ResetFilters() { CharacterComboBox.SelectedIndex = 0; BoxType.SelectedIndex = 0; ModeBox.SelectedIndex = 0; IdIndexBox.SelectedIndex = 0; MinimumIdBox.Text = ""; MaximumIdBox.Text = ""; RefreshItems(); }
}

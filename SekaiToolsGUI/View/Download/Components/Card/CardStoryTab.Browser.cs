using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Card;

public partial class CardStoryTab : IDownloadStoryBrowser
{
    private string _query = "";
    public event EventHandler? ResultsChanged;
    private IEnumerable<DownloadCandidate> FilteredCandidates => ViewModel.CardStories.SelectMany(story => StoryDownloadCandidates.Card(story, DownloadPageModel.Instance.CurrentSource));
    public IReadOnlyList<DownloadCandidate> VisibleDownloads => FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();
    public void ApplySearch(string query) { _query = query; RefreshItems(); }
    public void ResetFilters() { CharacterComboBox.SelectedIndex = 0; RarityComboBox.SelectedIndex = 0; RefreshItems(); }
}

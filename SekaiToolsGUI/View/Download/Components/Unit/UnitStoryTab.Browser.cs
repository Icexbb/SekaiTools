using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Unit;

public partial class UnitStoryTab : IDownloadStoryBrowser
{
    private string _query = "";
    public event EventHandler? ResultsChanged;
    private IEnumerable<DownloadCandidate> FilteredCandidates => SelectedChapters().SelectMany(chapter => StoryDownloadCandidates.Unit(chapter, DownloadPageModel.Instance.CurrentSource));
    public IReadOnlyList<DownloadCandidate> VisibleDownloads => FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();
    public void ApplySearch(string query) { _query = query; RefreshItems(); }
    public void ResetFilters() { UnitComboBox.SelectedIndex = 0; RefreshItems(); }
    private IEnumerable<SekaiDataFetch.Item.UnitStorySet.Chapter> SelectedChapters() =>
        UnitComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string unit } && ListUnitStory.Data.TryGetValue(unit, out var story)
            ? story.Chapters : Enumerable.Empty<SekaiDataFetch.Item.UnitStorySet.Chapter>();
}

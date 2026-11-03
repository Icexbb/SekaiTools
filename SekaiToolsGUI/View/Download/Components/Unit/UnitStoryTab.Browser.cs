using System.Windows.Controls;
using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Unit;

public partial class UnitStoryTab : IDownloadStoryBrowser
{
    private string _query = "";

    private IEnumerable<DownloadCandidate> FilteredCandidates => SelectedChapters().SelectMany(chapter =>
        StoryDownloadCandidates.Unit(chapter, DownloadPageModel.Instance.CurrentSource));

    public event EventHandler? ResultsChanged;

    public IReadOnlyList<DownloadCandidate> VisibleDownloads =>
        FilteredCandidates.Where(candidate => candidate.Matches(_query)).ToArray();

    public void ApplySearch(string query)
    {
        _query = query;
        RefreshItems();
    }

    public void ResetFilters()
    {
        UnitComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    private IEnumerable<UnitStorySet.Chapter> SelectedChapters()
    {
        return UnitComboBox.SelectedItem is ComboBoxItem { Tag: string unit } &&
               ListUnitStory.Data.TryGetValue(unit, out var story)
            ? story.Chapters
            : Enumerable.Empty<UnitStorySet.Chapter>();
    }
}
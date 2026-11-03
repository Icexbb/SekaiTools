using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Unit;

public partial class UnitStoryTab : UserControl, IRefreshable
{
    public UnitStoryTab()
    {
        InitializeComponent();
    }

    private ListUnitStory ListUnitStory => ListUnitStory.Instance;

    public async Task Refresh(IProgress<ListRefreshProgress>? progress = null)
    {
        UnitComboBox.IsEnabled = false;
        try
        {
            ListUnitStory.SetSource(GetSourceType());
            ListUnitStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await ListUnitStory.Refresh(progress);
            RefreshItems();
        }
        finally
        {
            UnitComboBox.IsEnabled = true;
        }
    }

    private void UnitComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void RefreshItems()
    {
        if (CardContents == null) return;

        CardContents.Children.Clear();
        foreach (var chapter in SelectedChapters().Where(chapter => StoryDownloadCandidates.Unit(chapter,
                     DownloadPageModel.Instance.CurrentSource).Any(candidate => candidate.Matches(_query))))
        {
            var chapterItem = new UnitStoryChapter(chapter)
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            CardContents.Children.Add(chapterItem);
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UnitStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (UnitComboBox.SelectedIndex < 0) UnitComboBox.SelectedIndex = 0;
        else RefreshItems();
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Special;

public partial class SpecialStoryTab : UserControl, IRefreshable
{
    public SpecialStoryTab()
    {
        InitializeComponent();
    }

    private ListSpecialStory ListSpecialStory => ListSpecialStory.Instance;

    public async Task Refresh()
    {
        SpecialStoryTypeSelector.IsEnabled = false;
        ListSpecialStory.SetSource(GetSourceType());
        ListSpecialStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ListSpecialStory.Refresh();
        RefreshCombo();
        SpecialStoryTypeSelector.IsEnabled = true;
    }


    private void RefreshCombo()
    {
        CardContents.Children.Clear();
        if (ListSpecialStory.Data.Count == 0) return;
        foreach (var (key, _) in ListSpecialStory.Data)
            SpecialStoryTypeSelector.Items.Add(key);

        SpecialStoryTypeSelector.SelectedIndex = 0;
    }

    private void RefreshSelection()
    {
        if (ListSpecialStory.Data.Count == 0) return;
        if (SpecialStoryTypeSelector.SelectedIndex == -1) return;
        var selectedUnit = SpecialStoryTypeSelector.SelectedItem.ToString()!;
        CardContents.Children.Clear();
        var set = ListSpecialStory.Data[selectedUnit];
        foreach (var episode in set.Episodes)
        {
            var item = DownloadItem.GetItem(() => SourceList.Instance.SpecialStory(episode), episode.Title);
            item.Margin = new Thickness(10, 5, 10, 5);
            CardContents.Children.Add(item);
        }
    }

    private void SpecialStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCombo();
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }

    private void SpecialStoryTypeSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelection();
    }
}
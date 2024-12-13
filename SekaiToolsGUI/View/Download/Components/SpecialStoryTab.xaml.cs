using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.View.Download.Components;

public partial class SpecialStoryTab : UserControl, IRefreshable
{
    public SpecialStoryTab()
    {
        InitializeComponent();
    }

    private ListSpecialStory? ListSpecialStory { get; set; }

    public async Task Refresh()
    {
        SpecialStoryTypeSelector.IsEnabled = false;
        ListSpecialStory ??= new ListSpecialStory(GetSourceType(), SettingPageModel.Instance.GetProxy());
        await ListSpecialStory.Refresh();
        RefreshCombo();
        SpecialStoryTypeSelector.IsEnabled = true;
    }


    private void RefreshCombo()
    {
        CardContents.Children.Clear();
        if (ListSpecialStory == null || ListSpecialStory.Data.Count == 0) return;
        foreach (var (key, value) in ListSpecialStory.Data)
        {
            SpecialStoryTypeSelector.Items.Add(key);
        }

        SpecialStoryTypeSelector.SelectedIndex = 0;
    }

    private void RefreshSelection()
    {
        if (ListSpecialStory == null || ListSpecialStory.Data.Count == 0) return;
        if (SpecialStoryTypeSelector.SelectedIndex == -1) return;
        var selectedUnit = SpecialStoryTypeSelector.SelectedItem.ToString()!;
        CardContents.Children.Clear();
        foreach (var episode in ListSpecialStory.Data[selectedUnit].Episodes)
        {
            var item = new DownloadItem(
                episode.Url(GetSourceType()),
                episode.Title)
            {
                Margin = new Thickness(10, 5, 10, 5)
            };
            CardContents.Children.Add(item);
        }
    }

    private void SpecialStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        ListSpecialStory = new ListSpecialStory(GetSourceType(), SettingPageModel.Instance.GetProxy());
        RefreshCombo();
    }

    private SourceList.SourceType GetSourceType()
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
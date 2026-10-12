using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Special;

public partial class SpecialStoryTab : UserControl, IRefreshable
{
    public SpecialStoryTab()
    {
        InitializeComponent();
    }

    private ListSpecialStory ListSpecialStory => ListSpecialStory.Instance;

    public async Task Refresh(IProgress<ListRefreshProgress>? progress = null)
    {
        SpecialStoryTypeSelector.IsEnabled = false;
        ListSpecialStory.SetSource(GetSourceType());
        ListSpecialStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ListSpecialStory.Refresh(progress);
        RefreshCombo();
        SpecialStoryTypeSelector.IsEnabled = true;
    }


    private void RefreshCombo()
    {
        CardContents.Children.Clear();
        CardContents.RowDefinitions.Clear();
        CardContents.ColumnDefinitions.Clear();
        if (ListSpecialStory.Data.Count == 0) return;
        foreach (var (key, _) in ListSpecialStory.Data)
            SpecialStoryTypeSelector.Items.Add(key);

        SpecialStoryTypeSelector.SelectedIndex = 0;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (ListSpecialStory.Data.Count == 0) return;
        if (SpecialStoryTypeSelector.SelectedIndex == -1) return;
        var selectedUnit = SpecialStoryTypeSelector.SelectedItem.ToString()!;
        CardContents.Children.Clear();
        CardContents.RowDefinitions.Clear();
        CardContents.ColumnDefinitions.Clear();
        var set = ListSpecialStory.Data[selectedUnit];
        const int itemPreRow = 4;
        for (var column = 0; column < itemPreRow; column++)
            CardContents.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var index = 0;
        foreach (var episode in set.Episodes)
        {
            var item = DownloadItem.GetItem(() => SourceList.Instance.SpecialStory(episode),
                episode.Title,
                episode.ScenarioId,
                $"SpecialStory|{set.SpecialStory.AssetBundleName}-{set.SpecialStory.Title}|{episode.ScenarioId}-{episode.Title}"
            );
            if (index % itemPreRow == 0)
                CardContents.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(item, index / itemPreRow);
            Grid.SetColumn(item, index % itemPreRow);
            item.Margin = new Thickness(0, 0, index % itemPreRow == itemPreRow - 1 ? 0 : 5, 5);
            CardContents.Children.Add(item);
            index++;
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

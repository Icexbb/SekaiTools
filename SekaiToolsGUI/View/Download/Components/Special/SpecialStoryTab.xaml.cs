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
        var selected = SpecialStoryTypeSelector.SelectedItem as string;
        ClearItems();
        SpecialStoryTypeSelector.Items.Clear();
        foreach (var key in ListSpecialStory.Data.Keys)
            SpecialStoryTypeSelector.Items.Add(key);
        if (selected != null && ListSpecialStory.Data.ContainsKey(selected))
            SpecialStoryTypeSelector.SelectedItem = selected;
        else if (SpecialStoryTypeSelector.Items.Count > 0)
            SpecialStoryTypeSelector.SelectedIndex = 0;
    }

    private void ClearItems()
    {
        foreach (var item in CardContents.Children.OfType<DownloadItem>().ToArray()) item.Recycle();
        StoryInformation.Visibility = Visibility.Collapsed;
        StartTimeText.Text = EndTimeText.Text = string.Empty;
    }

    private void RefreshSelection()
    {
        ClearItems();
        if (SpecialStoryTypeSelector.SelectedItem is not string selected ||
            !ListSpecialStory.Data.TryGetValue(selected, out var set)) return;
        StartTimeText.Text = FormatTime(set.SpecialStory.StartAt);
        EndTimeText.Text = FormatTime(set.SpecialStory.EndAt);
        StoryInformation.Visibility = Visibility.Visible;

        foreach (var episode in set.Episodes)
        {
            var item = DownloadItem.GetItem(() => SourceList.Instance.SpecialStory(episode),
                episode.Title,
                episode.ScenarioId,
                $"SpecialStory|{set.SpecialStory.AssetBundleName}-{set.SpecialStory.Title}|{episode.ScenarioId}-{episode.Title}"
            );
            item.Margin = new Thickness(0, 0, 0, 5);
            CardContents.Children.Add(item);
        }
    }

    private static string FormatTime(long timestamp)
    {
        if (timestamp <= 0) return "未提供";
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException) { return "时间无效"; }
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

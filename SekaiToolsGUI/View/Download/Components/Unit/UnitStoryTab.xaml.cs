using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Unit;

public partial class UnitStoryTab : UserControl, IRefreshable
{
    public UnitStoryTab()
    {
        InitializeComponent();
    }

    private ListUnitStory ListUnitStory => ListUnitStory.Instance;

    public async Task Refresh()
    {
        UnitComboBox.IsEnabled = false;
        try
        {
            ListUnitStory.SetSource(GetSourceType());
            ListUnitStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await ListUnitStory.Refresh();
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
        CardContents.Children.Clear();
        if (ListUnitStory.Data.Count == 0 ||
            UnitComboBox.SelectedItem is not ComboBoxItem { Tag: string selectedUnit } ||
            !ListUnitStory.Data.TryGetValue(selectedUnit, out var unitStory))
            return;

        foreach (var chapter in unitStory.Chapters)
        {
            var chapterItem = new UnitStoryChapter(chapter)
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            CardContents.Children.Add(chapterItem);
        }
    }

    private void UnitStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (UnitComboBox.SelectedIndex < 0) UnitComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using SekaiDataFetch.List;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.ViewModel.Download;
using SekaiToolsAvalonia.ViewModel.Setting;

namespace SekaiToolsAvalonia.View.Download.Components.Unit;

public partial class UnitStoryTab : UserControl, IRefreshable
{
    public UnitStoryTab()
    {
        InitializeComponent();
        RadioLightSound.IsChecked = true;

        RadioLightSound.IsCheckedChanged += RadioButton_OnChecked;
        RadioIdol.IsCheckedChanged += RadioButton_OnChecked;
        RadioThemePark.IsCheckedChanged += RadioButton_OnChecked;
        RadioStreet.IsCheckedChanged += RadioButton_OnChecked;
        RadioSchoolRefusal.IsCheckedChanged += RadioButton_OnChecked;
        RadioPiapro.IsCheckedChanged += RadioButton_OnChecked;
    }

    private static ListUnitStory ListUnitStory => ListUnitStory.Instance;

    public async Task Refresh()
    {
        ListUnitStory.SetSource(DownloadPageModel.Instance.CurrentSource);
        ListUnitStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ListUnitStory.Refresh();
        RefreshItems();
    }

    private void RadioButton_OnChecked(object? sender, RoutedEventArgs? e)
    {
        var selectedUnit = ((RadioButton)sender!).Name switch
        {
            "RadioLightSound" => "light_sound",
            "RadioIdol" => "idol",
            "RadioThemePark" => "theme_park",
            "RadioStreet" => "street",
            "RadioSchoolRefusal" => "school_refusal",
            "RadioPiapro" => "piapro",
            _ => "light_sound"
        };

        CardContents.Children.Clear();
        if (ListUnitStory.Data.Count == 0) return;

        if (ListUnitStory.Data.TryGetValue(selectedUnit, out var unitData))
        {
            foreach (var chapter in unitData.Chapters)
                foreach (var episode in chapter.Episodes)
                {
                    var item = new DownloadItem(
                        chapter.Name + "\n" + episode.Key,
                        () => SekaiDataFetch.Source.SourceList.Instance.UnitStory(
                            episode.ScenarioId, chapter.AssetBundleName))
                    { Margin = new Avalonia.Thickness(10, 5) };
                    CardContents.Children.Add(item);
                }
        }
    }

    private void RefreshItems()
    {
        if (ListUnitStory.Data.Count == 0) return;
        RadioButton_OnChecked(RadioLightSound, null);
    }
}

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
        CardUnits.IsEnabled = false;
        ListUnitStory.SetSource(GetSourceType());
        ListUnitStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ListUnitStory.Refresh();
        RefreshItems();
        CardUnits.IsEnabled = true;
    }


    private void RadioButton_OnChecked(object sender, RoutedEventArgs e)
    {
        var selectedUnit = ((RadioButton)sender).Name switch
        {
            "RadioLightSound" => "light_sound",
            "RadioIdol" => "idol",
            "RadioThemePark" => "theme_park",
            "RadioStreet" => "street",
            "RadioSchoolRefusal" => "school_refusal",
            "RadioPiapro" => "piapro",
            _ => throw new ArgumentOutOfRangeException()
        };
        CardContents.Children.Clear();
        if (ListUnitStory.Data.Count == 0) return;
        foreach (var chapter in ListUnitStory.Data[selectedUnit].Chapters)
        foreach (var episode in chapter.Episodes)
        {
            var item = DownloadItem.GetItem(
                () => SourceList.Instance.UnitStory(episode.ScenarioId, chapter.AssetBundleName),
                chapter.Name + "\n" + episode.Key);
            item.Margin = new Thickness(10, 5, 10, 5);
            CardContents.Children.Add(item);
        }
    }

    private void RefreshItems()
    {
        if (ListUnitStory.Data.Count == 0) return;
        if (RadioLightSound.IsChecked == true)
        {
            RadioButton_OnChecked(RadioLightSound, null!);
        }
        else if (RadioIdol.IsChecked == true)
        {
            RadioButton_OnChecked(RadioIdol, null!);
        }
        else if (RadioThemePark.IsChecked == true)
        {
            RadioButton_OnChecked(RadioThemePark, null!);
        }
        else if (RadioStreet.IsChecked == true)
        {
            RadioButton_OnChecked(RadioStreet, null!);
        }
        else if (RadioSchoolRefusal.IsChecked == true)
        {
            RadioButton_OnChecked(RadioSchoolRefusal, null!);
        }
        else if (RadioPiapro.IsChecked == true)
        {
            RadioButton_OnChecked(RadioPiapro, null!);
        }
        else
        {
            RadioLightSound.IsChecked = true;
            RefreshItems();
        }
    }

    private void UnitStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshItems();
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }
}
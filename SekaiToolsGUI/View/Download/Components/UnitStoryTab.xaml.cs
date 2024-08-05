using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Download.Tabs;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.View.Download.Components;

public partial class UnitStoryTab : UserControl, IRefreshable
{
    public UnitStoryTab()
    {
        InitializeComponent();
    }

    private ListUnitStory? ListUnitStory { get; set; }

    public async Task Refresh()
    {
        CardUnits.IsEnabled = false;
        if (ListUnitStory == null)
        {
            var settings = new SettingPageModel();
            settings.LoadSetting();
            ListUnitStory = new ListUnitStory(GetSourceType(), settings.GetProxy());
        }

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
        if (ListUnitStory == null || ListUnitStory.Data.Count == 0) return;
        foreach (var chapter in ListUnitStory.Data[selectedUnit].Chapters)
        foreach (var episode in chapter.Episodes)
        {
            var item = new DownloadItem(
                episode.Url(chapter.AssetBundleName, GetSourceType()),
                chapter.Name + "\n" + episode.Key)
            {
                Margin = new Thickness(10, 5, 10, 5)
            };
            CardContents.Children.Add(item);
        }
    }

    private void RefreshItems()
    {
        if (ListUnitStory == null || ListUnitStory.Data.Count == 0) return;
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
        var settings = new SettingPageModel();
        settings.LoadSetting();
        ListUnitStory = new ListUnitStory(GetSourceType(), settings.GetProxy());
        RefreshItems();
    }

    private SourceList.SourceType GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }
}
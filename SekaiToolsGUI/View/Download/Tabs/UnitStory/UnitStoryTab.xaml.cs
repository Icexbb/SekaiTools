using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download.Tabs.UnitStory;

public partial class UnitStoryTab : UserControl
{
    private ListUnitStory? ListUnitStory { get; set; }

    public UnitStoryTab()
    {
        InitializeComponent();
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

        foreach (var chapter in ListUnitStory!.Data[selectedUnit].Chapters)
        {
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
    }

    private async void UnitStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = new SettingPageModel();
        settings.LoadSetting();
        ListUnitStory = new ListUnitStory(GetSourceType(), settings.GetProxy());
        CardUnits.IsEnabled = false;
        await ListUnitStory.Refresh();
        CardUnits.IsEnabled = true;
    }

    private SourceList.SourceType GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }
}
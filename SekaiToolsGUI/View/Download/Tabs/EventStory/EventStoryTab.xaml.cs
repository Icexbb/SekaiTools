using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.View.Download.Tabs.EventStory;

public class ComboBoxData(string boxText, string listSource, bool isImage = true)
{
    public string BoxText { get; set; } = boxText;
    public string ListSource { get; set; } = listSource;

    public bool IsImage { get; set; } = isImage;
}

public class ComboBoxItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate TextTemplate { get; set; }
    public DataTemplate ImageTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is ComboBoxData comboBoxData)
        {
            return comboBoxData.IsImage ? ImageTemplate : TextTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}

public partial class EventStoryTab : UserControl
{
    private ListEventStory? ListEventStory { get; set; }
    public ObservableCollection<ComboBoxData> BoxUnitData { get; set; }
    public ComboBoxData SelectedUnit { get; set; }

    public EventStoryTab()
    {
        InitializeComponent();
        BoxUnitData =
        [
            new ComboBoxData("不指定", "不指定", false),
            new ComboBoxData("", "pack://application:,,,/Resource/Unit/logo_light_sound.png"),
            new ComboBoxData("", "pack://application:,,,/Resource/Unit/logo_idol.png"),
            new ComboBoxData("", "pack://application:,,,/Resource/Unit/logo_theme_park.png"),
            new ComboBoxData("", "pack://application:,,,/Resource/Unit/logo_street.png"),
            new ComboBoxData("", "pack://application:,,,/Resource/Unit/logo_school_refusal.png"),
            new ComboBoxData("混活", "混活", false)
        ];
        SelectedUnit = BoxUnitData[0];
        DataContext = this;
    }


    private void BoxUnit_OnSelected(object sender, RoutedEventArgs e)
    {
        if (BoxUnit.SelectedIndex == 0)
        {
            RefreshItems(ListEventStory!.Data);
            return;
        }

        var unitName = BoxUnit.SelectedIndex switch
        {
            1 => "light_sound",
            2 => "idol",
            3 => "theme_park",
            4 => "street",
            5 => "school_refusal",
            6 => "none",
            _ => throw new ArgumentOutOfRangeException()
        };
        RefreshItems(ListEventStory!.Data.Where(x => x.GameEvent.Unit == unitName));
    }

    private void BoxType_OnSelected(object sender, RoutedEventArgs e)
    {
        if (BoxUnit.SelectedIndex == 0)
        {
            RefreshItems(ListEventStory!.Data);
            return;
        }

        var unitName = BoxUnit.SelectedIndex switch
        {
            1 => "marathon",
            2 => "cheerful_carnival",
            3 => "world_bloom",
            _ => throw new ArgumentOutOfRangeException()
        };
        RefreshItems(ListEventStory!.Data.Where(x => x.GameEvent.EventType == unitName));
    }

    private void RefreshItems(IEnumerable<ListEventStory.EventStoryImpl> data)
    {
        Dispatcher.Invoke(() =>
        {
            CardContents.Children.Clear();
            foreach (var eventStory in data)
            {
                var item = new EventStoryEvent(eventStory, GetSourceType())
                {
                    Margin = new Thickness(10, 5, 10, 5)
                };
                CardContents.Children.Add(item);
            }
        });
    }

    private async void EventStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = new SettingPageModel();
        settings.LoadSetting();
        ListEventStory = new(GetSourceType(), settings.GetProxy());
        CardUnits.IsEnabled = false;
        await ListEventStory.Refresh();
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
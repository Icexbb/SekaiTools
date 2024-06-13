using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.View.Download.Tabs.EventStory;

public class EventStoryTabModel : ViewModelBase
{
    public bool CheckBoxChara01
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara02
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara03
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara04
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara05
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara06
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara07
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara08
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara09
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara10
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara11
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara12
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara13
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara14
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara15
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara16
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara17
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara18
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara19
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara20
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara21
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara22
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara23
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara24
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara25
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara26
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara27
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara28
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara29
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara30
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara31
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }
}

public partial class EventStoryTab : UserControl, IRefreshable

{
    private ListEventStory? ListEventStory { get; set; }

    private EventStoryTabModel Model => (EventStoryTabModel)DataContext;

    public EventStoryTab()
    {
        InitializeComponent();
        DataContext = new EventStoryTabModel();
    }

    private void Filter_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        RefreshItemsDisplay();
    }

    private void RefreshItemsDisplay()
    {
        if (CardContents == null) return;
        if (CardContents.Children.Count == 0) return;
        foreach (var obj in CardContents.Children)
        {
            if (obj is not EventStoryEvent item) continue;
            List<string> filterType = BoxType.SelectedIndex switch
            {
                0 => ["marathon", "cheerful_carnival", "world_bloom"],
                1 => ["marathon"],
                2 => ["cheerful_carnival"],
                3 => ["world_bloom"],
                _ => throw new ArgumentOutOfRangeException()
            };
            item.Visibility = filterType.Contains(item.EventStoryImpl.GameEvent.EventType)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (item.Visibility == Visibility.Visible)
            {
                item.Visibility = item.EventStoryImpl.GameEvent.EventType switch
                {
                    "marathon" or "cheerful_carnival" => item.EventStoryImpl.EventStory.BannerGameCharacterUnitId switch
                    {
                        1 => Model.CheckBoxChara01 ? Visibility.Visible : Visibility.Collapsed,
                        2 => Model.CheckBoxChara02 ? Visibility.Visible : Visibility.Collapsed,
                        3 => Model.CheckBoxChara03 ? Visibility.Visible : Visibility.Collapsed,
                        4 => Model.CheckBoxChara04 ? Visibility.Visible : Visibility.Collapsed,
                        5 => Model.CheckBoxChara05 ? Visibility.Visible : Visibility.Collapsed,
                        6 => Model.CheckBoxChara06 ? Visibility.Visible : Visibility.Collapsed,
                        7 => Model.CheckBoxChara07 ? Visibility.Visible : Visibility.Collapsed,
                        8 => Model.CheckBoxChara08 ? Visibility.Visible : Visibility.Collapsed,
                        9 => Model.CheckBoxChara09 ? Visibility.Visible : Visibility.Collapsed,
                        10 => Model.CheckBoxChara10 ? Visibility.Visible : Visibility.Collapsed,
                        11 => Model.CheckBoxChara11 ? Visibility.Visible : Visibility.Collapsed,
                        12 => Model.CheckBoxChara12 ? Visibility.Visible : Visibility.Collapsed,
                        13 => Model.CheckBoxChara13 ? Visibility.Visible : Visibility.Collapsed,
                        14 => Model.CheckBoxChara14 ? Visibility.Visible : Visibility.Collapsed,
                        15 => Model.CheckBoxChara15 ? Visibility.Visible : Visibility.Collapsed,
                        16 => Model.CheckBoxChara16 ? Visibility.Visible : Visibility.Collapsed,
                        17 => Model.CheckBoxChara17 ? Visibility.Visible : Visibility.Collapsed,
                        18 => Model.CheckBoxChara18 ? Visibility.Visible : Visibility.Collapsed,
                        19 => Model.CheckBoxChara19 ? Visibility.Visible : Visibility.Collapsed,
                        20 => Model.CheckBoxChara20 ? Visibility.Visible : Visibility.Collapsed,
                        21 => Model.CheckBoxChara21 ? Visibility.Visible : Visibility.Collapsed,
                        22 => Model.CheckBoxChara22 ? Visibility.Visible : Visibility.Collapsed,
                        23 => Model.CheckBoxChara23 ? Visibility.Visible : Visibility.Collapsed,
                        24 => Model.CheckBoxChara24 ? Visibility.Visible : Visibility.Collapsed,
                        25 => Model.CheckBoxChara25 ? Visibility.Visible : Visibility.Collapsed,
                        26 => Model.CheckBoxChara26 ? Visibility.Visible : Visibility.Collapsed,
                        27 => Model.CheckBoxChara27 ? Visibility.Visible : Visibility.Collapsed,
                        28 => Model.CheckBoxChara28 ? Visibility.Visible : Visibility.Collapsed,
                        29 => Model.CheckBoxChara29 ? Visibility.Visible : Visibility.Collapsed,
                        30 => Model.CheckBoxChara30 ? Visibility.Visible : Visibility.Collapsed,
                        31 => Model.CheckBoxChara31 ? Visibility.Visible : Visibility.Collapsed,
                        _ => item.Visibility
                    },
                    "world_bloom" => item.EventStoryImpl.EventStory.BannerGameCharacterUnitId switch
                    {
                        1 or 2 or 3 or 4 => Model.CheckBoxChara01 || Model.CheckBoxChara02 || Model.CheckBoxChara03 ||
                                            Model.CheckBoxChara04 || Model.CheckBoxChara27
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        5 or 6 or 7 or 8 => Model.CheckBoxChara05 || Model.CheckBoxChara06 || Model.CheckBoxChara07 ||
                                            Model.CheckBoxChara08 || Model.CheckBoxChara28
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        9 or 10 or 11 or 12 => Model.CheckBoxChara09 || Model.CheckBoxChara10 ||
                                               Model.CheckBoxChara11 || Model.CheckBoxChara12 || Model.CheckBoxChara29
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        13 or 14 or 15 or 16 => Model.CheckBoxChara13 || Model.CheckBoxChara14 ||
                                                Model.CheckBoxChara15 || Model.CheckBoxChara16 || Model.CheckBoxChara30
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        17 or 18 or 19 or 20 => Model.CheckBoxChara17 || Model.CheckBoxChara18 ||
                                                Model.CheckBoxChara19 || Model.CheckBoxChara20 || Model.CheckBoxChara31
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        21 or 22 or 23 or 24 or 25 or 26 => Model.CheckBoxChara21 || Model.CheckBoxChara22 ||
                                                            Model.CheckBoxChara23 || Model.CheckBoxChara24 ||
                                                            Model.CheckBoxChara25 || Model.CheckBoxChara26
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        _ => item.Visibility
                    },
                    _ => item.Visibility
                };
            }

            item.Margin = new Thickness(item.Visibility == Visibility.Visible ? 5 : 0);
        }
    }

    private void RefreshItems()
    {
        if (CardContents == null) return;
        CardContents.Children.Clear();
        if (ListEventStory == null || ListEventStory.Data.Count == 0) return;
        ChangeAllBannerSelector(true);
        var items = ListEventStory.Data.Select(impl => new EventStoryEvent(impl, GetSourceType()));
        items = items.OrderBy(x => x.EventStoryImpl.EventStory.EventId);
        if (_currentDirection == -1)
            items = items.Reverse();

        Dispatcher.BeginInvoke((Action)delegate()
        {
            foreach (var item in items)
            {
                CardContents.Children.Add(item);
            }
        });
    }

    private void EventStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = new SettingPageModel();
        settings.LoadSetting();
        ListEventStory = new ListEventStory(GetSourceType(), settings.GetProxy());
        BoxType.SelectedIndex = 0;
        RefreshItems();
    }

    public async Task Refresh()
    {
        CardUnits.IsEnabled = false;
        if (ListEventStory == null)
        {
            var settings = new SettingPageModel();
            settings.LoadSetting();
            ListEventStory = new ListEventStory(GetSourceType(), settings.GetProxy());
        }

        await ListEventStory.Refresh();
        RefreshItems();
        ChangeAllBannerSelector(true);
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


    private void CheckBoxUnit_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        switch (checkBox.Name)
        {
            case "CheckBoxUnitLn":
            {
                CheckBoxChara1.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara2.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara3.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara4.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara27.IsChecked = CheckBoxUnitLn.IsChecked;
            }
                break;
            case "CheckBoxUnitMmj":
            {
                CheckBoxChara5.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara6.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara7.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara8.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara28.IsChecked = CheckBoxUnitMmj.IsChecked;
            }
                break;
            case "CheckBoxUnitVbs":
            {
                CheckBoxChara9.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara10.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara11.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara12.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara29.IsChecked = CheckBoxUnitVbs.IsChecked;
            }
                break;
            case "CheckBoxUnitWs":
            {
                CheckBoxChara13.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara14.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara15.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara16.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara30.IsChecked = CheckBoxUnitWs.IsChecked;
            }
                break;
            case "CheckBoxUnit25":
            {
                CheckBoxChara17.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara18.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara19.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara20.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara31.IsChecked = CheckBoxUnit25.IsChecked;
            }
                break;
            case "CheckBoxUnitPiapro":
            {
                CheckBoxChara21.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara22.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara23.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara24.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara25.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara26.IsChecked = CheckBoxUnitPiapro.IsChecked;
            }
                break;
        }

        RefreshItemsDisplay();
    }

    private void CheckBoxChara_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;
        EnsureUnitBox();
        RefreshItemsDisplay();
    }

    private void EnsureUnitBox()
    {
        CheckBoxUnitLn.IsChecked = CheckBoxChara1.IsChecked == true && CheckBoxChara2.IsChecked == true &&
                                   CheckBoxChara3.IsChecked == true && CheckBoxChara4.IsChecked == true &&
                                   CheckBoxChara27.IsChecked == true;
        CheckBoxUnitMmj.IsChecked = CheckBoxChara5.IsChecked == true && CheckBoxChara6.IsChecked == true &&
                                    CheckBoxChara7.IsChecked == true && CheckBoxChara8.IsChecked == true &&
                                    CheckBoxChara28.IsChecked == true;
        CheckBoxUnitVbs.IsChecked = CheckBoxChara9.IsChecked == true && CheckBoxChara10.IsChecked == true &&
                                    CheckBoxChara11.IsChecked == true && CheckBoxChara12.IsChecked == true &&
                                    CheckBoxChara29.IsChecked == true;
        CheckBoxUnitWs.IsChecked = CheckBoxChara13.IsChecked == true && CheckBoxChara14.IsChecked == true &&
                                   CheckBoxChara15.IsChecked == true && CheckBoxChara16.IsChecked == true &&
                                   CheckBoxChara30.IsChecked == true;
        CheckBoxUnit25.IsChecked = CheckBoxChara17.IsChecked == true && CheckBoxChara18.IsChecked == true &&
                                   CheckBoxChara19.IsChecked == true && CheckBoxChara20.IsChecked == true &&
                                   CheckBoxChara31.IsChecked == true;
        CheckBoxUnitPiapro.IsChecked = CheckBoxChara21.IsChecked == true && CheckBoxChara22.IsChecked == true &&
                                       CheckBoxChara23.IsChecked == true && CheckBoxChara24.IsChecked == true &&
                                       CheckBoxChara25.IsChecked == true && CheckBoxChara26.IsChecked == true;
    }

    private void ButtonSelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        ChangeAllBannerSelector(true);
    }

    private void ButtonUnSelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        ChangeAllBannerSelector(false);
    }

    private void ChangeAllBannerSelector(bool to)
    {
        CheckBoxChara1.IsChecked = to;
        CheckBoxChara2.IsChecked = to;
        CheckBoxChara3.IsChecked = to;
        CheckBoxChara4.IsChecked = to;

        CheckBoxChara5.IsChecked = to;
        CheckBoxChara6.IsChecked = to;
        CheckBoxChara7.IsChecked = to;
        CheckBoxChara8.IsChecked = to;

        CheckBoxChara9.IsChecked = to;
        CheckBoxChara10.IsChecked = to;
        CheckBoxChara11.IsChecked = to;
        CheckBoxChara12.IsChecked = to;

        CheckBoxChara13.IsChecked = to;
        CheckBoxChara14.IsChecked = to;
        CheckBoxChara15.IsChecked = to;
        CheckBoxChara16.IsChecked = to;

        CheckBoxChara17.IsChecked = to;
        CheckBoxChara18.IsChecked = to;
        CheckBoxChara19.IsChecked = to;
        CheckBoxChara20.IsChecked = to;

        CheckBoxChara21.IsChecked = to;
        CheckBoxChara22.IsChecked = to;
        CheckBoxChara23.IsChecked = to;
        CheckBoxChara24.IsChecked = to;
        CheckBoxChara25.IsChecked = to;
        CheckBoxChara26.IsChecked = to;

        CheckBoxChara27.IsChecked = to;
        CheckBoxChara28.IsChecked = to;
        CheckBoxChara29.IsChecked = to;
        CheckBoxChara30.IsChecked = to;
        CheckBoxChara31.IsChecked = to;

        CheckBoxUnitLn.IsChecked = to;
        CheckBoxUnitMmj.IsChecked = to;
        CheckBoxUnitVbs.IsChecked = to;
        CheckBoxUnitWs.IsChecked = to;
        CheckBoxUnit25.IsChecked = to;
        CheckBoxUnitPiapro.IsChecked = to;

        RefreshItemsDisplay();
    }


    private int _currentDirection = -1;

    private void ButtonSort_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonSort.RenderTransform = new ScaleTransform(-1, _currentDirection);
        _currentDirection *= -1;
        RefreshItems();
    }
}
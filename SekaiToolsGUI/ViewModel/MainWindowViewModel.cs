using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.View.Subtitle;
using SekaiToolsGUI.View.Suppress;
using SekaiToolsGUI.View.Translate;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.ViewModel;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        try
        {
            SettingPageModel.LoadSetting();
        }
        catch (Exception)
        {
            // 设置加载失败，使用默认值继续运行
        }

        foreach (var item in NavigationItems.Concat(NavigationFooterItems).OfType<NavigationViewItem>())
        {
            var key = item.TargetPageType == typeof(TranslatePage) ? "Translation"
                : item.TargetPageType == typeof(SubtitlePage) ? "Subtitle"
                : item.TargetPageType == typeof(SuppressPage) ? "Encoding"
                : item.TargetPageType == typeof(DownloadPage) ? "StoryDownload" : "Settings";
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, $"MainWindow.Navigation.{key}");
            System.Windows.Data.BindingOperations.SetBinding(item, System.Windows.Automation.AutomationProperties.ItemStatusProperty,
                new System.Windows.Data.Binding(nameof(NavigationViewItem.IsActive))
                { Source = item, Converter = new CurrentPageStatusConverter() });
        }
    }

    private sealed class CurrentPageStatusConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is true ? "当前页面" : "";
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            System.Windows.Data.Binding.DoNothing;
    }

    public object[] NavigationItems { get; set; } =
    [
        new NavigationViewItem
        {
            Content = "自动轴机",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Subtitles24 },
            TargetPageType = typeof(SubtitlePage),
            NavigationCacheMode = NavigationCacheMode.Required
        },
        new NavigationViewItem
        {
            Content = "脚本翻译",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Translate24 },
            TargetPageType = typeof(TranslatePage),
            NavigationCacheMode = NavigationCacheMode.Required
        },
        new NavigationViewItem
        {
            Content = "数据下载",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowDownload24 },
            TargetPageType = typeof(DownloadPage),
            NavigationCacheMode = NavigationCacheMode.Required
        },
        new NavigationViewItem
        {
            Content = "视频压制",
            Icon = new SymbolIcon { Symbol = SymbolRegular.AnimalTurtle24 },
            TargetPageType = typeof(SuppressPage),
            NavigationCacheMode = NavigationCacheMode.Required
        }
    ];

    public object[] NavigationFooterItems { get; set; } =
    [
        new NavigationViewItem
        {
            Content = "设置与关于",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
            TargetPageType = typeof(SettingPage),
            NavigationCacheMode = NavigationCacheMode.Disabled
        }
    ];

    public static SettingPageModel SettingPageModel => SettingPageModel.Instance;
}

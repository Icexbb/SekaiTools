using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.View.Subtitle;
using SekaiToolsGUI.View.Suppress;
using SekaiToolsGUI.View.Translate;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.ViewModel;

public class MainWindowViewModel : ViewModelBase
{
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

    public MainWindowViewModel()
    {
        SettingPageModel.LoadSetting();
    }
}
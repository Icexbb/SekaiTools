using SekaiToolsAvalonia.View.Setting;
using SekaiToolsAvalonia.View.Subtitle;
using SekaiToolsAvalonia.View.Translate;
using SekaiToolsAvalonia.View.Download;
using SekaiToolsAvalonia.View.Suppress;
using SekaiToolsAvalonia.ViewModel.Setting;

namespace SekaiToolsAvalonia.ViewModel;

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
    }

    public NavItem[] NavigationItems { get; set; } =
    [
        new()
        {
            Content = "自动轴机",
            Icon = "字幕",
            TargetPageType = typeof(SubtitlePage),
            CachePage = true
        },
        new()
        {
            Content = "脚本翻译",
            Icon = "翻译",
            TargetPageType = typeof(TranslatePage),
            CachePage = true
        },
        new()
        {
            Content = "数据下载",
            Icon = "下载",
            TargetPageType = typeof(DownloadPage),
            CachePage = true
        },
        new()
        {
            Content = "视频压制",
            Icon = "压制",
            TargetPageType = typeof(SuppressPage),
            CachePage = true
        }
    ];

    public NavItem[] NavigationFooterItems { get; set; } =
    [
        new()
        {
            Content = "设置与关于",
            Icon = "设置",
            TargetPageType = typeof(SettingPage),
            CachePage = false
        }
    ];

    public NavItem? SelectedNavItem
    {
        get => GetProperty<NavItem?>(null);
        set => SetProperty(value);
    }
}

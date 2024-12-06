using System.Windows;
using System.Windows.Media.Imaging;
using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.View.Subtitle;
using SekaiToolsGUI.View.Suppress;
using SekaiToolsGUI.View.Translate;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI;

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

    public SettingPageModel SettingPageModel { get; } = new();
}

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closed += (sender, args) =>
        {
            Suppressor.Instance.Clean();
        };
    }

    public ISnackbarService WindowSnackbarService { get; } = new SnackbarService
    {
        DefaultTimeOut = TimeSpan.FromSeconds(3)
    };

    public IContentDialogService WindowContentDialogService { get; } = new ContentDialogService();

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowSnackbarService.SetSnackbarPresenter(SnackbarPresenter);
        WindowContentDialogService.SetDialogHost(RootContentDialog);
    }


    private void NavigationView_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationView.IsPaneOpen = false;
        if (NavigationView.MenuItems.Count != 0)
            NavigationView.Navigate((NavigationView.MenuItems[0] as NavigationViewItem)?.TargetPageType!);
    }
}
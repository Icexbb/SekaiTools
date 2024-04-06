using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SekaiToolsGUI.View;
using SekaiToolsGUI.View.Subtitle;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Markup;

namespace SekaiToolsGUI;

public class MainWindowViewModel : ViewModelBase
{
    public string Title { get; } = "Sekai Tools";

    public bool IsPaneOpen
    {
        get => GetProperty(false);
        set => SetProperty(value);
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
            NavigationCacheMode = NavigationCacheMode.Required
        },
        new NavigationViewItem
        {
            Content = "数据下载",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowDownload24 },
            NavigationCacheMode = NavigationCacheMode.Required
        }
    ];

    public object[] NavigationFooterItems { get; set; } =
    [
        new NavigationViewItem
        {
            Content = "切换主题",
            Icon = new SymbolIcon { Symbol = SymbolRegular.DarkTheme24 },
            Command = new ToggleThemeCommand(),
            NavigationCacheMode = NavigationCacheMode.Disabled
        },
        new NavigationViewItem
        {
            Content = "设置",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
            NavigationCacheMode = NavigationCacheMode.Disabled
        },
        new NavigationViewItem
        {
            Content = "关于",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
            NavigationCacheMode = NavigationCacheMode.Disabled
        },
    ];
}

public class ToggleThemeCommand : ICommand
{
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        var theme = ApplicationThemeManager.GetAppTheme();
        theme = theme == ApplicationTheme.Dark
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(theme);
        ApplicationAccentColorManager.Apply(SystemThemeManager.GlassColor);
    }
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
}

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        DataContext = new MainWindowViewModel();
        InitializeComponent();
        SetToSystemTheme();
    }

    private static void SetTheme(ApplicationTheme theme)
    {
        ApplicationThemeManager.Apply(theme);
    }

    private static void SetToSystemTheme()
    {
        var systemTheme = ApplicationThemeManager.GetSystemTheme();
        ApplicationAccentColorManager.Apply(SystemThemeManager.GlassColor);
        Console.WriteLine($"System Theme: {systemTheme}");
        switch (systemTheme)
        {
            case SystemTheme.Light:
                SetTheme(ApplicationTheme.Light);
                break;
            case SystemTheme.Dark:
                SetTheme(ApplicationTheme.Dark);
                break;
            case SystemTheme.HCWhite:
            case SystemTheme.HCBlack:
            case SystemTheme.HC1:
            case SystemTheme.HC2:
                SetTheme(ApplicationTheme.HighContrast);
                break;
            case SystemTheme.Unknown:
            case SystemTheme.Custom:
            case SystemTheme.Glow:
            case SystemTheme.CapturedMotion:
            case SystemTheme.Sunrise:
            case SystemTheme.Flow:
            default:
                SetTheme(ApplicationTheme.Unknown);
                break;
        }
    }

    public ISnackbarService WindowSnackbarService { get; } = new SnackbarService()
    {
        DefaultTimeOut = TimeSpan.FromSeconds(3)
    };

    public IContentDialogService WindowContentDialogService { get; } = new ContentDialogService();


    private void NavigationView_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationView.IsPaneOpen = false;
        if (NavigationView.MenuItems.Count != 0)
            NavigationView.Navigate((NavigationView.MenuItems[0] as NavigationViewItem)?.TargetPageType!);
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowSnackbarService.SetSnackbarPresenter(SnackbarPresenter);
        WindowContentDialogService.SetContentPresenter(RootContentDialog);
    }

    private void NavigationView_OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (args.Page is UIElement element)
        {
            var vb = BindingOperations.GetBinding(element, HeightProperty);
            if (vb == null)
            {
                BindingOperations.SetBinding(element, HeightProperty,
                    new Binding("ActualHeight") { Source = NavigationView });
            }
        }
    }
}
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.View.Setting;
using SekaiToolsGUI.View.Subtitle;
using SekaiToolsGUI.View.Suppress;
using SekaiToolsGUI.View.Translate;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SekaiToolsGUI;

public partial class MainWindow : FluentWindow
{
    private readonly EditorSessionStore _sessions = new();

    private readonly WindowsNotifyingSnackbarService _windowSnackbarService = new()
    {
        DefaultTimeOut = TimeSpan.FromSeconds(3)
    };

    private readonly bool _useCheckUpdate;
    private bool _closeConfirmed;
    private bool _closeInProgress;
    private object? _currentNavigatedPage;
    private TranslatePage? _translationPage;

    public MainWindow() : this(true)
    {
    }

    internal MainWindow(bool checkUpdates)
    {
        Control.AdaptiveLayout.InitializeDialogSizing();
        _useCheckUpdate = checkUpdates;
        InitializeComponent();
        WindowWorkArea.Attach(this);
        DataContext = new MainWindowViewModel();
        Closing += MainWindow_OnClosing;
        ContentRendered += OnUpdateContentRendered;
        TaskbarItemInfo = new TaskbarItemInfo();
        SetWindowTitle("");
    }

    public ISnackbarService WindowSnackbarService => _windowSnackbarService;
    public ISnackbarService WindowInAppSnackbarService => _windowSnackbarService.InAppService;

    public IContentDialogService WindowContentDialogService { get; } = new ContentDialogService();

    public void SetWindowTitle(string? content)
    {
        var mainTitle = $"Sekai Tools {GetLocalVersion()}".Trim();
        var title = content?.Length == 0 ? mainTitle : $"{content} - {mainTitle}";

        TitleBar.Title = title;
        Title = title;
    }

    public void SetTaskbarProgressState(TaskbarItemProgressState state, double value)
    {
        TaskbarItemInfo ??= new TaskbarItemInfo();
        TaskbarItemInfo.ProgressValue = Math.Clamp(value, 0, 1);
        TaskbarItemInfo.ProgressState = state;
    }

    public void SetTaskbarProgressValue(double value)
    {
        TaskbarItemInfo ??= new TaskbarItemInfo();
        TaskbarItemInfo.ProgressValue = Math.Clamp(value, 0, 1);
    }

    public void Navigate(Type pageType)
    {
        NavigationView.Navigate(pageType);
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowSnackbarService.SetSnackbarPresenter(SnackbarPresenter);
        WindowContentDialogService.SetDialogHost(RootContentDialog);
    }


    private void NavigationView_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationView.IsPaneOpen = false;
        var items = NavigationView.MenuItems.OfType<NavigationViewItem>().ToArray();
        var pageType = _sessions.ResolveLastModule(items.Select(item => item.TargetPageType).OfType<Type>());
        if (pageType != null) NavigationView.Navigate(pageType);
        // NavigationView_Debug();
    }


    private void NavigationView_Debug()
    {
        NavigationView.Navigate(typeof(TranslatePage));
    }

    private void NavigationView_OnNavigating(NavigationView sender, NavigatingCancelEventArgs args)
    {
        if (_currentNavigatedPage is TranslatePage translatePage) translatePage.FlushDraft();
    }

    private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed) return;

        // Keep the initial close cancelled while asynchronous cleanup runs.
        // WPF raises Closing synchronously, so a direct Close() after await
        // can re-enter Window's closing state and throw.
        if (_closeInProgress)
        {
            e.Cancel = true;
            return;
        }

        _closeInProgress = true;
        e.Cancel = true;
        try
        {
            if (_translationPage is { } translatePage && !await translatePage.ConfirmCloseAsync())
            {
                _closeInProgress = false;
                _restartForUpdate = false;
                return;
            }

            if (_translationPage != null && !_translationPage.FlushDraft())
            {
                _closeInProgress = false;
                _restartForUpdate = false;
                return;
            }

            SetWindowTitle("正在停止任务");
            SetTaskbarProgressState(TaskbarItemProgressState.Indeterminate, 0);
            await SuppressPage.DisposeSuppressorAsync();
            await SubtitlePage.DisposeTasksAsync();
            await DownloadPage.StopDownloadsAsync();
            await FinishUpdateOnCloseAsync();

            // Let the current Closing event finish before requesting the
            // actual close. The second Closing event is allowed by the flag.
            await Dispatcher.InvokeAsync(
                () =>
                {
                    _closeConfirmed = true;
                    Close();
                },
                DispatcherPriority.ApplicationIdle);
        }
        catch
        {
            _closeInProgress = false;
            _closeConfirmed = false;
            _restartForUpdate = false;
            throw;
        }
    }

    private void NavigationView_OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        _currentNavigatedPage = args.Page;
        if (args.Page is TranslatePage translatePage) _translationPage = translatePage;
        _sessions.RecordModule(args.Page.GetType(), NavigationView.MenuItems.OfType<NavigationViewItem>()
            .Select(item => item.TargetPageType).OfType<Type>());
        ResourceManager.Instance.SetProxy(SettingPageModel.Instance.GetProxy());
        switch (args.Page)
        {
            case IAppPage<object> appPage:
                appPage.OnNavigatedTo();
                break;
        }
    }

    private void NavigateToSetting()
    {
        NavigationView.Navigate(typeof(SettingPage));

        GetNavigatePage<SettingPage>(typeof(SettingPage));
    }

    private T? GetNavigatePage<T>(Type type)
        where T : class
    {
        if (!type.IsInstanceOfType(_currentNavigatedPage)) return null;

        return _currentNavigatedPage as T;
    }

    public async void OnCheckResourceFailed(Exception e, Action retryAction, string content = "检查资源错误，是否重试或检查设置？")
    {
        var result = await ShowRetryDialog();
        switch (result)
        {
            case ContentDialogResult.Primary:
                retryAction();
                break;
            case ContentDialogResult.Secondary:
                NavigateToSetting();
                break;
            case ContentDialogResult.None:
            default:
                Application.Current.Shutdown();
                break;
        }

        async Task<ContentDialogResult> ShowRetryDialog()
        {
            var token = CancellationToken.None;
            var dialogResult = await WindowContentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "错误",
                    Content = content + "\n" + e.StackTrace,
                    PrimaryButtonText = "重试",
                    CloseButtonText = "退出",
                    SecondaryButtonText = "检查设置"
                }, token);
            return dialogResult;
        }
    }
}

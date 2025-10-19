using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using SekaiDataFetch;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using Suppressor = SekaiToolsGUI.Suppress.Suppressor;

namespace SekaiToolsGUI;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closed += (sender, args) => { Suppressor.Instance.Clean(); };
        ContentRendered += (sender, args) => { CheckUpdate(); };
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

    private void NavigationView_OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        switch (args.Page)
        {
            case IAppPage<object> appPage:
                appPage.OnNavigatedTo();
                break;
        }
    }
}

partial class MainWindow
{
    private async void CheckUpdate()
    {
        var needUpdate = await CheckForUpdateAsync();
        if (!needUpdate) return;
        if (!await ShowJudgeDialog()) return;

        LaunchUpdater();
        Application.Current.Shutdown(); // 关闭主程序，交给 Updater 更新
        return;

        async Task<bool> ShowJudgeDialog()
        {
            var dialogService = WindowContentDialogService!;
            var token = CancellationToken.None;
            var dialogResult = await dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "检测到新版本，是否自动升级",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否"
                }, token);
            return dialogResult == ContentDialogResult.Primary;
        }
    }

    private async Task<bool> CheckForUpdateAsync()
    {
                return true;
        try
        {
            // 1. 获取本地版本
            var localVersion = GetLocalVersion();
            // 2. 获取远程版本
            var remoteVersion = await GetLatestVersionAsync();

            // 3. 比较
            if (new Version(remoteVersion) > new Version(localVersion))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            // 这里可以写日志，但不要影响启动
            Debug.WriteLine("检查更新失败: " + ex.Message);

            WindowSnackbarService.Show("错误", "运行结束", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
        }

        return false;
    }

    private static string GetLocalVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "0.0.0";
    }

    private static async Task<string> GetLatestVersionAsync()
    {
        const string url = "https://api.github.com/repos/Icexbb/SekaiTools/releases/latest";
        var proxy = SettingPageModel.Instance.GetProxy();

        using var client = new HttpClient(GetHttpHandler(proxy));

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SekaiToolsGUI", "1.0"));
        var json = await client.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.GetProperty("tag_name").GetString();
        return version?.TrimStart('v') ?? "0.0.0";

        HttpMessageHandler GetHttpHandler(Proxy proxyInfo)
        {
            return proxyInfo.ProxyType switch
            {
                Proxy.Type.None or Proxy.Type.System => new HttpClientHandler(),
                Proxy.Type.Http => new HttpClientHandler
                {
                    Proxy = new WebProxy(proxyInfo.Host, proxyInfo.Port), UseProxy = true
                },
                Proxy.Type.Socks5 => new SocketsHttpHandler
                {
                    Proxy = new WebProxy(proxyInfo.Host, proxyInfo.Port), UseProxy = true
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    private static void LaunchUpdater()
    {
        var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
        if (File.Exists(updaterPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("检测到新版本，但 Updater.exe 不存在！", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
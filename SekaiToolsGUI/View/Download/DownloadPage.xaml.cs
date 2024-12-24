using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch;
using SekaiToolsGUI.View.Download.Components;
using SekaiToolsGUI.View.Download.Components.Event;
using SekaiToolsGUI.View.Download.Components.Special;
using SekaiToolsGUI.View.Download.Components.Unit;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download;

public partial class DownloadPage : UserControl
{
    public DownloadPage()
    {
        InitializeComponent();
        BoxStoryType.SelectedIndex = 0;
        StoryTypeSelector_OnSelected(null!, null!);
    }

    private UnitStoryTab UnitStoryTab { get; } = new();
    private EventStoryTab EventStoryTab { get; } = new();
    private SpecialStoryTab SpecialStoryTab { get; } = new();

    public void AddTask(string tag, string url)
    {
        Dispatcher.Invoke(() => { DownloadItemBox.Items.Add(new DownloadTask(tag, url)); });
    }

    private void StoryTypeSelector_OnSelected(object sender, RoutedEventArgs e)
    {
        SelectIndex(BoxStoryType.SelectedIndex);
    }

    private void SelectIndex(int index)
    {
        ContentCard.Content = index switch
        {
            0 => UnitStoryTab,
            1 => EventStoryTab,
            2 => SpecialStoryTab,
            _ => null
        };
    }

    private void DownloadPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        BoxSource.SelectedIndex = 0;
    }

    private async void DownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        await FuncDownload();
        button.Content = "下载";
        foreach (var item in DownloadItemBox.Items)
        {
            if (item is not DownloadTask downloadItem) continue;
            if (!downloadItem.Downloaded)
                button.Content = "重试";
        }

        return;

        async Task FuncDownload()
        {
            button.IsEnabled = false;
            var savePath = "";
            foreach (var item in DownloadItemBox.Items)
            {
                if (item is not DownloadTask downloadItem) continue;
                savePath = Path.GetDirectoryName(downloadItem.SavePath)!;
                if (downloadItem.Downloaded) continue;
                downloadItem.ChangeStatus(0);
                try
                {
                    await Download(downloadItem.Url, downloadItem.SavePath);
                    downloadItem.ChangeStatus(1);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                    downloadItem.ChangeStatus(2);
                }
            }

            button.IsEnabled = true;
            if (savePath.Length != 0)
                ShowFile(savePath);
            return;

            void ShowFile(string path)
            {
                var psi = new ProcessStartInfo("Explorer.exe")
                {
                    Arguments = "/e," + path
                };
                Process.Start(psi);
            }
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        DownloadItemBox.Items.Clear();
    }

    private static async Task Download(string url, string filepath)
    {
        var client = new HttpClient(GetHttpHandler());
        Console.WriteLine($"GET {url}");
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var saveDir = Path.GetDirectoryName(filepath);
        if (saveDir != null && !Directory.Exists(saveDir))
            Directory.CreateDirectory(saveDir);
        await File.WriteAllTextAsync(filepath, responseContent);
        return;

        HttpMessageHandler GetHttpHandler()
        {
            var proxy = SettingPageModel.Instance.GetProxy();
            return proxy.ProxyType switch
            {
                Proxy.Type.None => new HttpClientHandler(),
                Proxy.Type.System => new HttpClientHandler(),
                Proxy.Type.Http => new HttpClientHandler
                {
                    Proxy = new WebProxy(proxy.Host, proxy.Port), UseProxy = true
                },
                Proxy.Type.Socks5 => new SocketsHttpHandler
                {
                    Proxy = new WebProxy(proxy.Host, proxy.Port), UseProxy = true
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public SourceList.SourceType GetSourceType()
    {
        return BoxSource.SelectedIndex switch
        {
            0 => SourceList.SourceType.SiteBest,
            1 => SourceList.SourceType.SiteAi,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async void ButtonRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ContentCard.Content is not IRefreshable refreshable) return;
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
            var dialog = new RefreshWaitDialog();
            var source = new CancellationTokenSource();
            _ = dialogService.ShowAsync(dialog, source.Token);
            await refreshable.Refresh();
            await source.CancelAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show("刷新失败: " + exception.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            if (Debugger.IsAttached) throw;
        }
    }
}
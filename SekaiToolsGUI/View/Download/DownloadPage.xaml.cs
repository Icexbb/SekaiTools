using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.View.Download;

public partial class DownloadPage : UserControl
{
    public DownloadPage()
    {
        InitializeComponent();
    }

    public void AddTask(string tag, string url)
    {
        Dispatcher.Invoke(() => { DownloadItemBox.Items.Add(new DownloadTask(tag, url)); });
    }

    private void StoryTypeSelector_OnSelected(object sender, RoutedEventArgs e)
    {
        ContentCard.Content = BoxStoryType.SelectedIndex switch
        {
            0 => new Tabs.UnitStory.UnitStoryTab(),
            1 => new Tabs.EventStory.EventStoryTab(),
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
            foreach (var item in DownloadItemBox.Items)
            {
                if (item is not DownloadTask downloadItem) continue;
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
            ShowFile(Path.Combine(Directory.GetCurrentDirectory(), "Scripts"));
            return;

            void ShowFile(string path)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
                {
                    Arguments = "/e," + path
                };
                System.Diagnostics.Process.Start(psi);
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
            var proxy = new SettingPageModel().GetProxy();
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
}
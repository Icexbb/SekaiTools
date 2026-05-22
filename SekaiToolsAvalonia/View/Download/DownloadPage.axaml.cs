using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using SekaiDataFetch.Source;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.ViewModel.Download;
using SekaiToolsAvalonia.ViewModel.Setting;

namespace SekaiToolsAvalonia.View.Download;

public partial class DownloadPage : UserControl, IAppPage
{
    private readonly Components.Unit.UnitStoryTab _unitTab = new();
    private readonly TextBlock _stubEvent = new() { Text = "活动剧情 - 开发中", Margin = new(10) };
    private readonly TextBlock _stubSpecial = new() { Text = "特殊剧情 - 开发中", Margin = new(10) };
    private readonly TextBlock _stubCard = new() { Text = "角色剧情 - 开发中", Margin = new(10) };
    private readonly TextBlock _stubAction = new() { Text = "地图对话 - 开发中", Margin = new(10) };

    public DownloadPage()
    {
        DataContext = DownloadPageModel.Instance;
        InitializeComponent();
        BoxStoryType.SelectedIndex = 0;
        SelectIndex(0);
    }

    private DownloadPageModel ViewModel => DownloadPageModel.Instance;

    public void OnNavigatedTo()
    {
        _ = InitDownloadSource();
    }

    public void AddTask(string tag, string url)
    {
        DownloadItemBox.Items.Add(new Components.DownloadTask(tag, url));
    }

    private void StoryTypeSelector_OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        SelectIndex(BoxStoryType.SelectedIndex);
    }

    private void SelectIndex(int index)
    {
        ContentCard.Child = index switch
        {
            0 => _unitTab,
            1 => _stubEvent,
            2 => _stubSpecial,
            3 => _stubCard,
            4 => _stubAction,
            _ => null
        };
    }

    private async void DownloadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = (Button)sender!;
        button.IsEnabled = false;

        foreach (var item in DownloadItemBox.Items)
        {
            if (item is not Components.DownloadTask task || task.Downloaded) continue;
            task.ChangeStatus(0);
            try
            {
                var content = await FetchString(task.Url);
                var filename = Path.GetFileName(task.Url);
                var savePath = Path.Combine(ResourceManager.DataBaseDir, filename);
                var dir = Path.GetDirectoryName(savePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(savePath, content);
                task.ChangeStatus(1);
            }
            catch (Exception ex)
            {
                Logger.Log($"下载失败: {ex.Message}", LogLevel.Error);
                task.ChangeStatus(2);
            }
        }

        button.IsEnabled = true;
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DownloadItemBox.Items.Clear();
    }

    private async void ButtonRefresh_OnClick(object? sender, RoutedEventArgs e)
    {
        await InitDownloadSource();
    }

    private async Task InitDownloadSource()
    {
        const string sourceListUrl = "https://config.g.xbb.moe/source.json";
        try
        {
            var json = await FetchString(sourceListUrl);
            var doc = JsonDocument.Parse(json);
            var sourceList = doc.RootElement.Deserialize<SourceData[]>()!;
            ViewModel.SourceData = sourceList;
        }
        catch (Exception e)
        {
            Logger.Log($"数据源获取失败: {e.Message}", LogLevel.Error);
            ViewModel.SourceData = SourceData.Default;
        }
    }

    private static async Task<string> FetchString(string url)
    {
        var proxy = SettingPageModel.Instance.GetProxy();
        using var handler = proxy.ProxyType switch
        {
            Proxy.Type.Http or Proxy.Type.Socks5 => new HttpClientHandler
            {
                Proxy = new WebProxy(proxy.Host, proxy.Port), UseProxy = true
            },
            _ => new HttpClientHandler()
        };
        using var client = new HttpClient(handler);
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

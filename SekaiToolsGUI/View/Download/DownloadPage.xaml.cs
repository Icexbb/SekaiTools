using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsConfiguration;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.Download.Components;
using SekaiToolsGUI.View.Download.Components.Action;
using SekaiToolsGUI.View.Download.Components.Card;
using SekaiToolsGUI.View.Download.Components.Event;
using SekaiToolsGUI.View.Download.Components.Special;
using SekaiToolsGUI.View.Download.Components.Unit;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.ViewModel.Download;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace SekaiToolsGUI.View.Download;

public partial class DownloadPage : UserControl, IAppPage<DownloadPageModel>
{
    public DownloadPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        BoxStoryType.SelectedIndex = 0;
        StoryTypeSelector_OnSelected(null!, null!);
    }

    private UnitStoryTab UnitStoryTab { get; } = new();
    private EventStoryTab EventStoryTab { get; } = new();
    private SpecialStoryTab SpecialStoryTab { get; } = new();
    private CardStoryTab CardStoryTab { get; } = new();
    private ActionStoryTab ActionStoryTab { get; } = new();
    public DownloadPageModel ViewModel => DownloadPageModel.Instance;

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;


    public void OnNavigatedTo()
    {
        InitDownloadSource();
    }


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
            3 => CardStoryTab,
            4 => ActionStoryTab,
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
            TaskClearButton.IsEnabled = false;
            ContentCard.IsEnabled = false;
            var tasks = DownloadItemBox.Items.OfType<DownloadTask>().ToArray();
            var savePath = "";
            var successCount = 0;
            var failures = new List<string>();
            try
            {
                foreach (var downloadItem in tasks)
                {
                    if (downloadItem.Downloaded) continue;
                    downloadItem.ChangeStatus(0);
                    try
                    {
                        await Fetcher.Instance.FetchToFile(downloadItem.Url, downloadItem.SavePath);
                        downloadItem.ChangeStatus(1);
                        successCount++;
                        savePath = Path.GetDirectoryName(downloadItem.SavePath)!;
                    }
                    catch (Exception exception)
                    {
                        downloadItem.ChangeStatus(2);
                        failures.Add(downloadItem.ScriptTag);
                        Log.Logger.LogError(exception, "Download {Tag} from {Url} failed",
                            downloadItem.ScriptTag, downloadItem.Url);
                    }
                }
            }
            finally
            {
                button.IsEnabled = true;
                TaskClearButton.IsEnabled = true;
                ContentCard.IsEnabled = true;
            }

            ShowDownloadSummary(successCount, failures);
            if (savePath.Length != 0)
                ShowFile(savePath);
            return;

            void ShowDownloadSummary(int succeeded, IReadOnlyCollection<string> failed)
            {
                if (succeeded == 0 && failed.Count == 0)
                {
                    SnackService.Show("提示", "没有待下载的任务。", ControlAppearance.Info,
                        new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(3));
                    return;
                }

                if (failed.Count == 0)
                {
                    SnackService.Show("下载完成", $"已成功下载 {succeeded} 个文件。", ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(4));
                    return;
                }

                var failedPreview = string.Join("、", failed.Take(3));
                if (failed.Count > 3) failedPreview += "等";
                SnackService.Show("下载完成",
                    $"成功 {succeeded} 个，失败 {failed.Count} 个：{failedPreview}。详情请查看运行日志。",
                    ControlAppearance.Caution, new SymbolIcon(SymbolRegular.DocumentDismiss24),
                    TimeSpan.FromSeconds(7));
            }

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

    public SourceData GetSourceType()
    {
        return ViewModel.CurrentSource;
    }

    private async void InitDownloadSource()
    {
        var sourceListUrl = NetworkEndpoints.Current.Resources.SourceListUrl;
        try
        {
            // structure : {data:SourceData[],keyword:string}
            var sourceListJson = await Fetcher.Instance.Fetch(sourceListUrl);
            var sourceListDoc = JsonDocument.Parse(sourceListJson);
            var sourceList = sourceListDoc.RootElement.Deserialize<SourceData[]>()!;
            ViewModel.SourceData = sourceList;
        }
        catch (Exception e)
        {
            SnackService.Show("错误", "数据源获取失败，已使用内置数据源。" + e.Message,
                ControlAppearance.Danger, new SymbolIcon(SymbolRegular.CloudDismiss24), TimeSpan.FromSeconds(5));
            Log.Logger.LogError(e, "{TypeName} InitDownloadSource Error", nameof(DownloadPage));
            ViewModel.SourceData = SourceData.Default;
            if (Debugger.IsAttached) throw;
        }
    }

    private async void ButtonRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        if (ContentCard.Content is not IRefreshable refreshable) return;

        var button = (Button)sender;
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在刷新下载源数据");
        using var source = new CancellationTokenSource();

        button.IsEnabled = false;
        _ = dialogService.ShowAsync(dialog, source.Token);
        try
        {
            await refreshable.Refresh();
        }
        catch (Exception exception)
        {
            SnackService.Show("刷新失败", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ArrowSyncDismiss24), TimeSpan.FromSeconds(5));
            Log.Logger.LogError(exception, "{TypeName} Refresh Error", nameof(DownloadPage));
            if (Debugger.IsAttached) throw;
        }
        finally
        {
            await source.CancelAsync();
            button.IsEnabled = true;
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiDataFetch.List;
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
    private static Task? _sourceListInitializationTask;
    private readonly Dictionary<int, UserControl> _storyTabs = new();
    private readonly Dictionary<int, Task> _storyInitializationTasks = new();
    private int _selectionVersion;

    public DownloadPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        BoxStoryType.SelectedIndex = 0;
    }

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    private static ISnackbarService QueueSnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowInAppSnackbarService;

    public DownloadPageModel ViewModel => DownloadPageModel.Instance;


    public async void OnNavigatedTo()
    {
        await Dispatcher.Yield(DispatcherPriority.Background);
        _sourceListInitializationTask ??= InitDownloadSourceAsync();
        await _sourceListInitializationTask;
    }


    public bool AddTask(string tag, string url)
    {
        return Dispatcher.Invoke(() =>
        {
            if (DownloadItemBox.Items.OfType<DownloadTask>().Any(item => item.Url == url))
            {
                QueueSnackService.Show("已在下载列表中", tag, ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
                return false;
            }

            var task = new DownloadTask(tag, url);
            task.RemoveRequested += DownloadTask_OnRemoveRequested;
            DownloadItemBox.Items.Add(task);
            UpdateTaskListState();
            QueueSnackService.Show("已加入下载列表", tag, ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.AddCircle24), TimeSpan.FromSeconds(2));
            return true;
        });
    }

    private void DownloadTask_OnRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not DownloadTask task) return;
        task.RemoveRequested -= DownloadTask_OnRemoveRequested;
        DownloadItemBox.Items.Remove(task);
        UpdateTaskListState();
    }

    public void AddTasks(IEnumerable<(string Tag, string Url)> candidates)
    {
        Dispatcher.Invoke(() =>
        {
            var urls = DownloadItemBox.Items.OfType<DownloadTask>().Select(task => task.Url).ToHashSet();
            var added = 0;
            var skipped = 0;
            foreach (var (tag, url) in candidates)
            {
                if (!urls.Add(url))
                {
                    skipped++;
                    continue;
                }
                var task = new DownloadTask(tag, url);
                task.RemoveRequested += DownloadTask_OnRemoveRequested;
                DownloadItemBox.Items.Add(task);
                added++;
            }
            UpdateTaskListState();
            QueueSnackService.Show("批量加入完成", $"已加入 {added} 条，跳过重复 {skipped} 条。", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));
        });
    }

    private void UpdateTaskListState()
    {
        var tasks = DownloadItemBox.Items.OfType<DownloadTask>().ToArray();
        TaskCountText.Text = $"下载列表 ({tasks.Length})";
        EmptyTaskPanel.Visibility = tasks.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        TaskClearButton.IsEnabled = tasks.Length > 0;
        DownloadButton.IsEnabled = tasks.Any(task => !task.Downloaded);
    }

    private async void StoryTypeSelector_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        await SelectIndexAsync(BoxStoryType.SelectedIndex);
    }

    private async Task SelectIndexAsync(int index)
    {
        if (ContentCard == null || index is < 0 or > 4) return;
        var version = ++_selectionVersion;
        if (_storyTabs.TryGetValue(index, out var existing))
        {
            ContentCard.Content = existing;
            LoadingPanel.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
            return;
        }

        ContentCard.Content = null;
        LoadingMessage.Text = "正在加载剧情列表…";
        LoadingPanel.Visibility = Visibility.Visible;
        RefreshButton.IsEnabled = false;
        try
        {
            // Render the page shell before reading caches or constructing the selected tab.
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (version != _selectionVersion) return;
            if (!_storyInitializationTasks.TryGetValue(index, out var initialization))
            {
                initialization = Task.Run(() => InitializeStoryData(index));
                _storyInitializationTasks.Add(index, initialization);
            }
            await initialization;
            if (version != _selectionVersion) return;

            // WPF controls stay on the UI thread; only non-UI cache loading runs in the worker.
            var tab = index switch
            {
                0 => (UserControl)new UnitStoryTab(),
                1 => new EventStoryTab(),
                2 => new SpecialStoryTab(),
                3 => new CardStoryTab(),
                4 => new ActionStoryTab(),
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
            _storyTabs.Add(index, tab);
            ContentCard.Content = tab;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            _storyInitializationTasks.Remove(index);
            Log.Logger.LogError(exception, "Download story tab {Index} initialization failed", index);
            if (version == _selectionVersion)
                LoadingMessage.Text = "列表加载失败，请点击“刷新当前列表”重试。";
        }
        finally
        {
            if (version == _selectionVersion) RefreshButton.IsEnabled = true;
        }
    }

    private static void InitializeStoryData(int index)
    {
        // Accessing the singleton first loads and parses its local JSON cache.
        _ = index switch
        {
            0 => (BaseListStory)ListUnitStory.Instance,
            1 => ListEventStory.Instance,
            2 => ListSpecialStory.Instance,
            3 => ListCardStory.Instance,
            4 => ListActionStory.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
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
            foreach (var task in tasks) task.SetCanRemove(false);
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
                ContentCard.IsEnabled = true;
                foreach (var task in tasks) task.SetCanRemove(true);
                UpdateTaskListState();
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
        foreach (var task in DownloadItemBox.Items.OfType<DownloadTask>())
            task.RemoveRequested -= DownloadTask_OnRemoveRequested;
        DownloadItemBox.Items.Clear();
        UpdateTaskListState();
    }

    public SourceData GetSourceType()
    {
        return ViewModel.CurrentSource;
    }

    private async Task InitDownloadSourceAsync()
    {
        var sourceListUrl = NetworkEndpoints.Current.Resources.SourceListUrl;
        try
        {
            // structure : {data:SourceData[],keyword:string}
            var sourceListJson = await Fetcher.Instance.Fetch(sourceListUrl);
            var sourceList = await Task.Run(() =>
            {
                var sources = JsonSerializer.Deserialize<SourceData[]>(sourceListJson);
                return sources is { Length: > 0 } ? sources : throw new InvalidDataException("数据源列表为空。");
            });
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
        if (ContentCard.Content is not IRefreshable refreshable)
        {
            await SelectIndexAsync(BoxStoryType.SelectedIndex);
            return;
        }

        var button = (Button)sender;
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在刷新下载源数据");
        using var source = new CancellationTokenSource();

        button.IsEnabled = false;
        _ = dialogService.ShowAsync(dialog, source.Token);
        try
        {
            var progress = new Progress<ListRefreshProgress>(dialog.UpdateProgress);
            await refreshable.Refresh(progress);
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

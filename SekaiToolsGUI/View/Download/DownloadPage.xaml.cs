using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
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
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;


namespace SekaiToolsGUI.View.Download;

public partial class DownloadPage : UserControl, IAppPage<DownloadPageModel>
{
    private static Task? _sourceListInitializationTask;
    private static readonly HashSet<DownloadPage> ActiveDownloads = [];
    private static readonly object StoryDataLock = new();
    private readonly DownloadBatch _batch = new(Fetcher.Instance);
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly Dictionary<string, DownloadCandidate> _selectedDownloads = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Task> _storyInitializationTasks = new();
    private readonly Dictionary<int, UserControl> _storyTabs = new();
    private HashSet<Guid> _batchIds = [];
    private IReadOnlyList<DownloadCandidate> _browserResults = [];
    private CancellationTokenSource? _downloadCancellation;
    private string? _downloadDirectory;
    private bool _queueExpanded;
    private Dictionary<string, DownloadTask> _queuedDownloads = new(StringComparer.Ordinal);
    private Task? _runningBatch;
    private int _selectionVersion;
    private int _sourceVersion;

    public DownloadPage() : this(true)
    {
    }

    internal DownloadPage(bool initializeCatalog)
    {
        InitializeComponent();
        DataContext = ViewModel;
        SizeChanged += (_, _) => UpdateQueueLayout();
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplyBrowserSearch();
        };
        if (initializeCatalog) BoxStoryType.SelectedIndex = 0;
    }

    private bool IsDownloading => _downloadCancellation != null;

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    private static ISnackbarService QueueSnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowInAppSnackbarService;

    public DownloadPageModel ViewModel => DownloadPageModel.Instance;


    public async void OnNavigatedTo()
    {
        DirectoryText.Text = _downloadDirectory ?? SettingPageModel.Instance.DownloadDirectory;
        await Dispatcher.Yield(DispatcherPriority.Background);
        _sourceListInitializationTask ??= InitDownloadSourceAsync();
        await _sourceListInitializationTask;
    }

    public event EventHandler? DownloadChoicesChanged;


    public bool AddTask(string tag, string url)
    {
        return Dispatcher.Invoke(() =>
        {
            if (DownloadItemBox.Items.OfType<DownloadTask>().Any(item => item.Url == url))
            {
                if (Application.Current.MainWindow is MainWindow)
                    QueueSnackService.Show("已在下载列表中", tag, ControlAppearance.Info,
                        new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
                return false;
            }

            if (!TryGetDownloadDirectory(out var directory)) return false;
            var task = new DownloadTask(tag, url, ViewModel.CurrentSource.SourceName, directory);
            task.RemoveRequested += DownloadTask_OnRemoveRequested;
            task.RetryRequested += DownloadTask_OnRetryRequested;
            DownloadItemBox.Items.Add(task);
            UpdateTaskListState();
            return true;
        });
    }

    private void DownloadTask_OnRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not DownloadTask task || (IsDownloading && _batchIds.Contains(task.Id))) return;
        task.RemoveRequested -= DownloadTask_OnRemoveRequested;
        task.RetryRequested -= DownloadTask_OnRetryRequested;
        DownloadItemBox.Items.Remove(task);
        UpdateTaskListState();
    }

    public void AddTasks(IEnumerable<(string Tag, string Url)> candidates)
    {
        Dispatcher.Invoke(() =>
        {
            if (!TryGetDownloadDirectory(out var directory)) return;
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

                var task = new DownloadTask(tag, url, ViewModel.CurrentSource.SourceName, directory);
                task.RemoveRequested += DownloadTask_OnRemoveRequested;
                task.RetryRequested += DownloadTask_OnRetryRequested;
                DownloadItemBox.Items.Add(task);
                added++;
            }

            UpdateTaskListState();
            if (Application.Current.MainWindow is MainWindow)
                QueueSnackService.Show("批量加入完成", $"已加入 {added} 条，跳过重复 {skipped} 条。", ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));
        });
    }

    private bool TryGetDownloadDirectory(out string directory)
    {
        try
        {
            directory = GetDownloadDirectory();
            return true;
        }
        catch (Exception error)
        {
            directory = "";
            QueueSnackService.Show("下载目录无效", error.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.FolderOpen24), TimeSpan.FromSeconds(5));
            return false;
        }
    }

    private string GetDownloadDirectory()
    {
        return _downloadDirectory ?? SettingPageModel.Instance.GetDownloadDirectory();
    }

    private void UpdateTaskListState()
    {
        var tasks = DownloadItemBox.Items.OfType<DownloadTask>().ToArray();
        _queuedDownloads = tasks.ToDictionary(task => task.Url, StringComparer.Ordinal);
        TaskCountText.Text = $"下载列表（{tasks.Length}）";
        var failedCount = tasks.Count(task => task.Status == DownloadStatus.Failed);
        if (failedCount > 0) TaskCountText.Text += $" · 失败 {failedCount}";
        EmptyTaskPanel.Visibility = tasks.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        TaskClearButton.IsEnabled = tasks.Length > 0 && !IsDownloading;
        FilePolicyBox.IsEnabled = ChooseDirectoryButton.IsEnabled = !IsDownloading;
        var waiting = tasks.Count(task => !task.Downloaded);
        DownloadButton.IsEnabled = IsDownloading ? !_downloadCancellation!.IsCancellationRequested : waiting > 0;
        DownloadActionText.Text = IsDownloading
            ? _downloadCancellation!.IsCancellationRequested ? "正在停止…" : "停止下载"
            : $"开始下载（{waiting}）";
        DownloadActionIcon.Symbol = IsDownloading ? SymbolRegular.Dismiss24 : SymbolRegular.ArrowDownload24;
        var failures = tasks.Count(task => task.Status == DownloadStatus.Failed);
        RetryFailedButton.Visibility = failures > 0 ? Visibility.Visible : Visibility.Collapsed;
        RetryFailedButton.IsEnabled = !IsDownloading;
        RetryFailedText.Text = $"重试失败项（{failures}）";
        DirectoryText.Text = _downloadDirectory ?? SettingPageModel.Instance.DownloadDirectory;
        UpdateBrowserSelection();
        DownloadChoicesChanged?.Invoke(this, EventArgs.Empty);
        if (tasks.Length == 0 && !IsDownloading)
        {
            BatchProgress.Maximum = 1;
            BatchProgress.Value = 0;
            BatchStatusText.Text = "选择剧情后开始下载";
        }

        if (IsDownloading)
        {
            var batch = tasks.Where(task => _batchIds.Contains(task.Id)).ToArray();
            var done = batch.Count(task =>
                task.Status is DownloadStatus.Succeeded or DownloadStatus.Skipped or DownloadStatus.Failed);
            BatchProgress.Maximum = Math.Max(1, batch.Length);
            BatchProgress.Value = done;
            BatchStatusText.Text =
                $"已处理 {done}/{batch.Length} · 失败 {batch.Count(task => task.Status == DownloadStatus.Failed)}";
            if (ActualWidth < 920 && !_queueExpanded) TaskCountText.Text += $" · {done}/{batch.Length}";
            var next = tasks.Count(task => !_batchIds.Contains(task.Id) && !task.Downloaded);
            if (next > 0) BatchStatusText.Text += $" · 下一批 {next}";
        }
    }

    private void UpdateQueueLayout()
    {
        if (QueueCard == null) return;
        var compact = ActualWidth < 920;
        Grid.SetRow(QueueCard, compact ? 2 : 0);
        Grid.SetColumn(QueueCard, compact ? 0 : 1);
        Grid.SetColumnSpan(QueueCard, compact ? 2 : 1);
        Grid.SetRowSpan(QueueCard, compact ? 1 : 2);
        QueueCard.Width = compact ? double.NaN : 320;
        var collapsed = compact && !_queueExpanded;
        QueueDetailsButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        QueueDetailsIcon.Symbol = _queueExpanded ? SymbolRegular.ChevronUp24 : SymbolRegular.ChevronDown24;
        QueueDetailsButton.ToolTip = _queueExpanded ? "收起下载列表" : "展开下载列表";
        QueueBody.Visibility = QueueFooter.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        QueueCard.Height = compact && !collapsed ? Math.Min(320, ActualHeight * 0.5) : double.NaN;
        QueueCard.Margin = compact ? new Thickness(0, 8, 0, 0) : new Thickness(12, 0, 0, 0);
        var target = collapsed ? QueueHeader : QueueFooter;
        if (!ReferenceEquals(MainActions.Parent, target))
        {
            ((Panel)MainActions.Parent).Children.Remove(MainActions);
            target.Children.Add(MainActions);
        }

        MainActions.Width = collapsed ? Math.Min(260, ActualWidth * 0.42) : double.NaN;
        Grid.SetColumnSpan(DirectoryGrid, compact ? 1 : 2);
        Grid.SetRow(FilePolicyBox, compact ? 2 : 3);
        Grid.SetColumn(FilePolicyBox, compact ? 1 : 0);
        Grid.SetColumnSpan(FilePolicyBox, compact ? 1 : 2);
        FilePolicyBox.Margin = compact ? new Thickness(8, 0, 0, 8) : new Thickness(0, 0, 0, 8);
        Grid.SetRow(RetryFailedButton, compact ? 3 : 4);
        Grid.SetColumnSpan(RetryFailedButton, compact ? 1 : 2);
        RetryFailedButton.Margin = compact ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 0, 8);
        Grid.SetRow(MainActions, collapsed ? 0 : compact ? 3 : 5);
        Grid.SetColumn(MainActions, compact ? 1 : 0);
        Grid.SetColumnSpan(MainActions, compact ? 1 : 2);
    }

    private void QueueDetails_OnClick(object sender, RoutedEventArgs e)
    {
        _queueExpanded = !_queueExpanded;
        UpdateQueueLayout();
    }

    private void Search_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BrowserBar == null) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ApplyBrowserSearch()
    {
        _searchTimer.Stop();
        if (ContentCard.Content is not IDownloadStoryBrowser browser) return;
        BrowserBar.IsEnabled = true;
        StoryBrowserState.SetQuery(ContentCard, SearchBox.Text.Trim());
        browser.ApplySearch(SearchBox.Text.Trim());
        UpdateBrowserResults();
    }

    private void Browser_OnResultsChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, ContentCard.Content)) UpdateBrowserResults();
    }

    private void UpdateBrowserResults()
    {
        if (ContentCard.Content is not IDownloadStoryBrowser browser) return;
        _browserResults = browser.VisibleDownloads.DistinctBy(candidate => candidate.Url).ToArray();
        var visible = _browserResults.Select(candidate => candidate.Url).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _selectedDownloads.Keys.Where(key => !visible.Contains(key)).ToArray())
            _selectedDownloads.Remove(key);
        BrowserEmptyPanel.Visibility = _browserResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BrowserEmptyText.Text = string.IsNullOrWhiteSpace(SearchBox.Text) ? "当前没有可下载结果" : "没有匹配的剧情";
        UpdateBrowserSelection();
        DownloadChoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateBrowserSelection()
    {
        if (BrowserResultText == null) return;
        foreach (var key in _selectedDownloads.Keys.Where(IsDownloadQueued).ToArray()) _selectedDownloads.Remove(key);
        BrowserResultText.Text = $"{_browserResults.Count} 项结果 · 已选 {_selectedDownloads.Count}";
        var available = _browserResults.Where(candidate => !IsDownloadQueued(candidate.Url)).ToArray();
        SelectResultsBox.IsEnabled = available.Length > 0;
        SelectResultsBox.IsChecked = available.Length > 0 &&
                                     available.All(candidate => _selectedDownloads.ContainsKey(candidate.Url));
        AddSelectedButton.IsEnabled = _selectedDownloads.Count > 0;
        AddSelectedText.Text = $"加入已选（{_selectedDownloads.Count}）";
    }

    public bool IsDownloadQueued(string url)
    {
        return _queuedDownloads.ContainsKey(url);
    }

    public bool IsDownloadSelected(string url)
    {
        return _selectedDownloads.ContainsKey(url);
    }

    public void SelectDownload(DownloadCandidate candidate, bool selected)
    {
        if (selected && !IsDownloadQueued(candidate.Url) && _browserResults.Any(result => result.Url == candidate.Url))
            _selectedDownloads[candidate.Url] = candidate;
        else _selectedDownloads.Remove(candidate.Url);
        UpdateBrowserSelection();
        DownloadChoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetDownloadState(string url)
    {
        _queuedDownloads.TryGetValue(url, out var task);
        if (task != null)
            return task.Status switch
            {
                DownloadStatus.Downloading => "下载中",
                DownloadStatus.Succeeded => "已下载",
                DownloadStatus.Failed => "失败",
                DownloadStatus.Stopped => "已停止",
                DownloadStatus.Skipped => "已跳过",
                _ => "已加入"
            };
        try
        {
            return File.Exists(Path.Combine(GetDownloadDirectory(), Path.GetFileName(new Uri(url).LocalPath)))
                ? "本地同名"
                : "";
        }
        catch (ArgumentException)
        {
            return "";
        }
    }

    private void SelectResults_OnClick(object sender, RoutedEventArgs e)
    {
        var select = SelectResultsBox.IsChecked == true;
        foreach (var candidate in _browserResults)
            if (select && !IsDownloadQueued(candidate.Url)) _selectedDownloads[candidate.Url] = candidate;
            else _selectedDownloads.Remove(candidate.Url);
        UpdateBrowserSelection();
        DownloadChoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddSelected_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _selectedDownloads.Values.ToArray();
        AddTasks(selected.Select(candidate => (candidate.Tag, candidate.Url)));
        UpdateBrowserSelection();
        DownloadChoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetSearch_OnClick(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        SearchBox.Text = "";
        _selectedDownloads.Clear();
        (ContentCard.Content as IDownloadStoryBrowser)?.ResetFilters();
        ApplyBrowserSearch();
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
            _selectedDownloads.Clear();
            ContentCard.Content = existing;
            ApplyBrowserSearch();
            LoadingPanel.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
            return;
        }

        ContentCard.Content = null;
        BrowserBar.IsEnabled = false;
        BrowserEmptyPanel.Visibility = Visibility.Collapsed;
        _selectedDownloads.Clear();
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
                var selectedSource = BoxSource.SelectedItem as SourceData ?? ViewModel.CurrentSource;
                var sourceVersion = _sourceVersion;
                initialization = Task.Run(() =>
                {
                    lock (StoryDataLock)
                    {
                        if (sourceVersion != _sourceVersion) return;
                        Fetcher.Instance.SetSource(selectedSource);
                        InitializeStoryData(index).ReloadFromCache();
                    }
                });
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
            if (tab is IDownloadStoryBrowser browser) browser.ResultsChanged += Browser_OnResultsChanged;
            ContentCard.Content = tab;
            ApplyBrowserSearch();
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            if (version == _selectionVersion) _storyInitializationTasks.Remove(index);
            Log.Logger.LogError(exception, "Download story tab {Index} initialization failed", index);
            if (version == _selectionVersion)
                LoadingMessage.Text = "列表加载失败，请点击“刷新当前列表”重试。";
        }
        finally
        {
            if (version == _selectionVersion) RefreshButton.IsEnabled = true;
        }
    }

    private static BaseListStory InitializeStoryData(int index)
    {
        // Accessing the singleton first loads and parses its local JSON cache.
        return index switch
        {
            0 => ListUnitStory.Instance,
            1 => ListEventStory.Instance,
            2 => ListSpecialStory.Instance,
            3 => ListCardStory.Instance,
            4 => ListActionStory.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    private void SourceSelector_OnTargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (e.Property == ItemsControl.ItemsSourceProperty)
            Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                var selectedIndex = ViewModel.CurrentSourceIndex;
                BoxSource.SetCurrentValue(Selector.SelectedIndexProperty, -1);
                BoxSource.SetCurrentValue(Selector.SelectedIndexProperty, selectedIndex);
            }));
    }

    private async void SourceSelector_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (BoxSource.SelectedItem is not SourceData || ContentCard == null) return;
        ViewModel.CurrentSourceIndex = BoxSource.SelectedIndex;
        ++_selectionVersion;
        ++_sourceVersion;
        _storyTabs.Clear();
        _storyInitializationTasks.Clear();
        await SelectIndexAsync(BoxStoryType.SelectedIndex);
    }

    private async void DownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsDownloading)
        {
            _downloadCancellation!.Cancel();
            UpdateTaskListState();
            return;
        }

        await StartBatchAsync(DownloadItemBox.Items.OfType<DownloadTask>().Where(task => !task.Downloaded).ToArray());
    }

    private async void DownloadTask_OnRetryRequested(object? sender, EventArgs e)
    {
        if (sender is DownloadTask task && !IsDownloading) await StartBatchAsync([task]);
    }

    private async void RetryFailed_OnClick(object sender, RoutedEventArgs e)
    {
        await StartBatchAsync(DownloadItemBox.Items.OfType<DownloadTask>()
            .Where(task => task.Status == DownloadStatus.Failed).ToArray());
    }

    private async Task StartBatchAsync(DownloadTask[] tasks)
    {
        if (IsDownloading || tasks.Length == 0) return;
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        ActiveDownloads.Add(this);
        _batchIds = tasks.Select(task => task.Id).ToHashSet();
        foreach (var task in tasks)
        {
            task.ChangeStatus(DownloadStatus.Waiting);
            task.SetCanRemove(false);
        }

        var policy = FilePolicyBox.SelectedIndex == 1 ? ExistingFilePolicy.Replace : ExistingFilePolicy.Skip;
        UpdateTaskListState();
        try
        {
            var byId = tasks.ToDictionary(task => task.Id);
            var run = _batch.RunAsync(tasks.Select(task => task.Job).ToArray(), policy,
                update => Dispatcher.Invoke(() =>
                {
                    byId[update.Job.Id].ChangeStatus(update.Status, update.Error);
                    UpdateTaskListState();
                }), cancellation.Token);
            _runningBatch = run;
            var result = await run;
            BatchStatusText.Text =
                $"成功 {result.Succeeded} · 跳过 {result.Skipped} · 失败 {result.Failed} · 停止 {result.Stopped}";
            if (Application.Current.MainWindow is MainWindow)
                QueueSnackService.Show(cancellation.IsCancellationRequested ? "下载已停止" : "本批下载结束",
                    BatchStatusText.Text, result.Failed > 0 ? ControlAppearance.Caution : ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.ArrowDownload24), TimeSpan.FromSeconds(5));
        }
        catch (Exception error)
        {
            Log.Logger.LogError(error, "Download batch failed");
            foreach (var task in tasks.Where(task =>
                         task.Status is DownloadStatus.Waiting or DownloadStatus.Downloading))
                task.ChangeStatus(DownloadStatus.Failed, DownloadBatch.DescribeError(error));
            BatchStatusText.Text = "本批下载未完成，请检查失败项后重试。";
        }
        finally
        {
            ActiveDownloads.Remove(this);
            _runningBatch = null;
            _downloadCancellation = null;
            foreach (var task in tasks) task.SetCanRemove(true);
            UpdateTaskListState();
        }
    }

    public static async Task StopDownloadsAsync()
    {
        var pages = ActiveDownloads.ToArray();
        foreach (var page in pages) page._downloadCancellation?.Cancel();
        await Task.WhenAll(pages.Select(page => page._runningBatch ?? Task.CompletedTask));
    }

    private void ChooseDirectory_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsDownloading) return;
        var dialog = new OpenFolderDialog
            { Title = "选择剧本下载目录", InitialDirectory = _downloadDirectory ?? Model.Setting.Default.DownloadDirectory };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        _downloadDirectory = dialog.FolderName;
        foreach (var task in DownloadItemBox.Items.OfType<DownloadTask>().Where(task => !task.Downloaded))
            task.SetDirectory(_downloadDirectory);
        UpdateTaskListState();
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsDownloading) return;
        foreach (var task in DownloadItemBox.Items.OfType<DownloadTask>())
        {
            task.RemoveRequested -= DownloadTask_OnRemoveRequested;
            task.RetryRequested -= DownloadTask_OnRetryRequested;
        }

        DownloadItemBox.Items.Clear();
        UpdateTaskListState();
    }

    private void OpenDownloadFolder_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = GetDownloadDirectory();
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Log.Logger.LogError(exception, "Open download folder failed");
            SnackService.Show("打开下载文件夹失败", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.FolderOpen24), TimeSpan.FromSeconds(5));
        }
    }

    private async void ButtonClearCache_OnClick(object sender, RoutedEventArgs e)
    {
        if (!BoxSource.IsEnabled || !ClearCacheButton.IsEnabled) return;
        if (_sourceListInitializationTask != null) await _sourceListInitializationTask;
        if (!BoxSource.IsEnabled || !ClearCacheButton.IsEnabled) return;

        var contentEnabled = ContentCard.IsEnabled;
        ClearCacheButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        BoxSource.IsEnabled = false;
        BoxStoryType.IsEnabled = false;
        ContentCard.IsEnabled = false;
        ++_selectionVersion;
        ++_sourceVersion;
        _storyTabs.Clear();
        _storyInitializationTasks.Clear();
        ContentCard.Content = null;
        LoadingMessage.Text = "正在清除列表缓存…";
        LoadingPanel.Visibility = Visibility.Visible;
        try
        {
            await Task.Run(() =>
            {
                lock (StoryDataLock)
                {
                    var cacheDirectory = Path.GetFullPath(
                        Path.Combine(BaseListStory.DataBaseDir, "Data", "cache"));
                    if (Directory.Exists(cacheDirectory))
                        Directory.Delete(cacheDirectory, true);
                }
            });
            SnackService.Show("缓存已清除", "已清除所有列表缓存，可点击“刷新当前列表”重新获取。",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.Delete24), TimeSpan.FromSeconds(4));
        }
        catch (Exception exception)
        {
            Log.Logger.LogError(exception, "{TypeName} ClearCache Error", nameof(DownloadPage));
            SnackService.Show("清除缓存失败", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
        finally
        {
            await SelectIndexAsync(BoxStoryType.SelectedIndex);
            ContentCard.IsEnabled = contentEnabled;
            BoxSource.IsEnabled = true;
            BoxStoryType.IsEnabled = true;
            ClearCacheButton.IsEnabled = true;
        }
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
        BoxSource.IsEnabled = false;
        BoxStoryType.IsEnabled = false;
        ClearCacheButton.IsEnabled = false;
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
            ClearCacheButton.IsEnabled = true;
            BoxSource.IsEnabled = true;
            BoxStoryType.IsEnabled = true;
            button.IsEnabled = true;
        }
    }
}
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.Subtitle.Components;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace SekaiToolsGUI.View.Subtitle;

public partial class SubtitlePage : UserControl, IAppPage<SubtitleQueuePageModel>
{
    private static SubtitlePage? _instance;
    private readonly SequentialTaskQueue<SubtitleQueueTaskModel> _queue;
    private bool _preparingResources;
    private bool _checkedSavedProgress;
    private bool _closing;
    private SubtitlePageModel? _selectedState;

    public SubtitlePage()
    {
        _queue = new SequentialTaskQueue<SubtitleQueueTaskModel>(RunTaskAsync);
        _queue.TaskFailed += (task, exception) =>
        {
            task.State.IsRunning = false;
            task.State.IsCanceling = false;
            task.State.HasNotStarted = false;
            task.State.IsFailed = true;
            ShowError("字幕任务失败", exception);
        };
        DataContext = new SubtitleQueuePageModel(_queue);
        ViewModel.PropertyChanged += QueuePageModel_OnPropertyChanged;
        InitializeComponent();
        GongDragDrop.SetDropHandler(PendingTasksList, new PendingTasksDropHandler(_queue));
        _instance = this;
    }

    public SubtitleQueuePageModel ViewModel => (SubtitleQueuePageModel)DataContext;
    private static IContentDialogService DialogService =>
        ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    public async void OnNavigatedTo()
    {
        RefreshSelectedTaskWindowState();
        if (_preparingResources || _closing || ViewModel.ResourcesReady) return;
        _preparingResources = true;
        try
        {
            await SubtitleTask.EnsureResourcesAsync();
            ViewModel.ResourcesReady = true;
            if (!_checkedSavedProgress)
            {
                _checkedSavedProgress = true;
                var saved = EnumerateSavedProgress().Where(item => MaterialsExist(item.State)).ToList();
                if (saved.Count > 0)
                {
                    ViewModel.IsDialogOpen = true;
                    var result = await DialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
                    {
                        Title = "恢复未完成任务",
                        Content = $"检测到 {saved.Count} 个未完成的字幕任务，是否加入队列继续处理？",
                        PrimaryButtonText = "加入队列",
                        CloseButtonText = "暂不恢复"
                    }, CancellationToken.None);
                    ViewModel.IsDialogOpen = false;
                    if (result == ContentDialogResult.Primary && !_closing)
                        foreach (var entry in saved) AddTask(CreateTask(entry.State, false));
                }
            }
        }
        catch (Exception exception)
        {
            ViewModel.ResourcesReady = false;
            (Application.Current.MainWindow as MainWindow)?.OnCheckResourceFailed(exception, OnNavigatedTo);
        }
        finally
        {
            _preparingResources = false;
            ViewModel.IsDialogOpen = false;
        }
    }

    private async void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanOpenDialog || _closing) return;
        ViewModel.IsDialogOpen = true;
        try
        {
            var dialog = new SubtitleTaskDialog(DialogService.GetDialogHostEx() ?? throw new InvalidOperationException());
            var result = await DialogService.ShowAsync(dialog, CancellationToken.None);
            if (result == ContentDialogResult.Primary && !_closing)
                AddTask(new SubtitleQueueTaskModel(dialog.ViewModel));
        }
        catch (Exception exception) { ShowError("添加任务失败", exception); }
        finally { ViewModel.IsDialogOpen = false; }
    }

    private void AddTask(SubtitleQueueTaskModel task)
    {
        // Never let duplicate live tasks overwrite each other's saved progress.
        if (_queue.PendingTasks.Concat(_queue.ProcessingTasks).Any(existing =>
                SameMaterials(existing.State, task.State)))
        {
            SnackService.Show("任务已存在", "相同素材的任务已经在队列中", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(3));
            task.Detach();
            return;
        }
        _queue.PendingTasks.Add(task);
        SelectTask(task);
        _ = AdvanceQueueAsync();
    }

    private async Task AdvanceQueueAsync()
    {
        try { await _queue.RunAsync(); }
        catch (Exception exception) { ShowError("字幕队列异常", exception); }
    }

    private static Task RunTaskAsync(SubtitleQueueTaskModel task) => task.Control.RunAsync(task.SavedState);

    private void QueuePageModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SubtitleQueuePageModel.SelectedTask)) return;
        if (_selectedState != null) _selectedState.PropertyChanged -= SelectedTaskState_OnPropertyChanged;
        _selectedState = ViewModel.SelectedTask?.State;
        if (_selectedState != null) _selectedState.PropertyChanged += SelectedTaskState_OnPropertyChanged;
        RefreshSelectedTaskWindowState();
    }

    private void SelectedTaskState_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SubtitlePageModel.Progress) or nameof(SubtitlePageModel.RunningStatus)
            or nameof(SubtitlePageModel.VideoFileName))
            RefreshSelectedTaskWindowState();
    }

    private void RefreshSelectedTaskWindowState()
    {
        if (_closing || Application.Current.MainWindow is not MainWindow window) return;
        var task = ViewModel.SelectedTask;
        if (task == null)
        {
            window.SetWindowTitle("");
            window.SetTaskbarProgressState(TaskbarItemProgressState.None, 0);
            return;
        }

        var state = task.State;
        window.SetWindowTitle($"{task.Status} - {task.VideoName}");
        var progressState = state.IsCanceled || state.IsPartial ? TaskbarItemProgressState.Paused
            : state.IsFinished ? TaskbarItemProgressState.Normal
            : state.IsFailed ? TaskbarItemProgressState.Error
            : state.IsCanceling ? TaskbarItemProgressState.Paused
            : state.IsRunning ? TaskbarItemProgressState.Normal
            : TaskbarItemProgressState.None;
        window.SetTaskbarProgressState(progressState,
            state.IsFinished ? 1 : progressState == TaskbarItemProgressState.None ? 0 : state.Progress);
    }

    private async void HistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanOpenDialog || _closing) return;
        ViewModel.IsDialogOpen = true;
        try
        {
            var entries = HistoryStore.LoadAll();
            var unfinished = EnumerateSavedProgress()
                .Select(item => new HistoryEntry { Timestamp = "未完成", State = item.State }).ToList();
            if (entries.Count == 0 && unfinished.Count == 0)
            {
                SnackService.Show("提示", "暂无历史记录", ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(3));
                return;
            }
            var dialog = new HistoryDialog(DialogService.GetDialogHostEx() ?? throw new InvalidOperationException(), entries, unfinished);
            var result = await DialogService.ShowAsync(dialog, CancellationToken.None);
            if (_closing) return;
            if (result == ContentDialogResult.Secondary)
            {
                var clear = await DialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
                {
                    Title = "清除已完成历史记录？", Content = "此操作不可恢复，但不会删除未完成任务。",
                    PrimaryButtonText = "清除", CloseButtonText = "取消"
                }, CancellationToken.None);
                if (clear == ContentDialogResult.Primary) HistoryStore.Clear();
                return;
            }
            var state = (dialog.SelectedEntry ?? dialog.SelectedUnfinishedEntry)?.State;
            if (result != ContentDialogResult.Primary || state == null) return;
            var task = CreateTask(state, true);
            task.Control.LoadHistoryState(state);
            _queue.HistoryTasks.Add(task);
            SelectTask(task);
        }
        catch (Exception exception) { ShowError("读取历史失败", exception); }
        finally { ViewModel.IsDialogOpen = false; }
    }

    private static SubtitleQueueTaskModel CreateTask(ProcessingState state, bool isHistory) => new(new SubtitlePageModel
    {
        VideoFilePath = state.VideoFilePath,
        ScriptFilePath = state.ScriptFilePath,
        TranslateFilePath = state.TranslateFilePath
    }, isHistory, state);

    private void SelectTask(SubtitleQueueTaskModel task)
    {
        task.Control.RemoveRequested -= TaskControl_OnRemoveRequested;
        task.Control.RemoveRequested += TaskControl_OnRemoveRequested;
        task.Control.CanDeleteSavedProgress = () => !task.IsHistory && !_queue.PendingTasks
            .Concat(_queue.ProcessingTasks).Any(other => SameMaterials(other.State, task.State));
        ViewModel.SelectedTask = task;
        RefreshSelectedTaskWindowState();
    }

    private void TaskItem_OnSelected(object? sender, EventArgs e)
    {
        if (sender is SubtitleQueueTaskItem { DataContext: SubtitleQueueTaskModel task }) SelectTask(task);
    }

    private async void TaskItem_OnRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not SubtitleQueueTaskItem { DataContext: SubtitleQueueTaskModel task } || task.State.IsRunning) return;
        if (!task.State.HasNotStarted)
        {
            var result = await DialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
            {
                Title = "移除字幕任务？", Content = "该任务的处理结果和手动调整将从列表清除，已保存的文件和历史记录仍会保留。",
                PrimaryButtonText = "移除", CloseButtonText = "取消"
            }, CancellationToken.None);
            if (result != ContentDialogResult.Primary) return;
        }
        await RemoveTaskAsync(task);
    }

    private async void TaskControl_OnRemoveRequested(object? sender, EventArgs e)
    {
        var task = AllTasks().FirstOrDefault(item => ReferenceEquals(item.CreatedControl, sender));
        if (task != null) await RemoveTaskAsync(task);
    }

    private async Task RemoveTaskAsync(SubtitleQueueTaskModel task)
    {
        if (_queue.ProcessingTasks.Contains(task)) return;
        _queue.PendingTasks.Remove(task);
        _queue.FinishedTasks.Remove(task);
        _queue.HistoryTasks.Remove(task);
        if (ReferenceEquals(ViewModel.SelectedTask, task))
        {
            ViewModel.SelectedTask = null;
            if (AllTasks().FirstOrDefault() is { } next) SelectTask(next);
        }
        if (task.CreatedControl is { } control)
        {
            control.RemoveRequested -= TaskControl_OnRemoveRequested;
            await control.DisposeAsync();
        }
        task.Detach();
    }

    private IEnumerable<SubtitleQueueTaskModel> AllTasks() => _queue.ProcessingTasks.Concat(_queue.PendingTasks)
        .Concat(_queue.FinishedTasks).Concat(_queue.HistoryTasks);

    public static async Task DisposeTasksAsync()
    {
        if (_instance is not { } page) return;
        page._closing = true;
        page.ViewModel.PropertyChanged -= page.QueuePageModel_OnPropertyChanged;
        if (page._selectedState != null)
            page._selectedState.PropertyChanged -= page.SelectedTaskState_OnPropertyChanged;
        page._selectedState = null;
        var stopped = page._queue.StopAsync();
        foreach (var task in page._queue.ProcessingTasks) task.CreatedControl?.RequestStop();
        await stopped;
        foreach (var task in page.AllTasks().ToArray())
        {
            if (task.CreatedControl is { } control) await control.DisposeAsync();
            task.Detach();
        }
        _instance = null;
    }

    private static bool MaterialsExist(ProcessingState state) => File.Exists(state.VideoFilePath) &&
        File.Exists(state.ScriptFilePath) && File.Exists(state.TranslateFilePath);

    private static IEnumerable<(string SaveKey, ProcessingState State)> EnumerateSavedProgress() =>
        SubtitleQueueProgressStore.EnumerateProgressFiles().Concat(ProgressStore.EnumerateProgressFiles()
            .Where(entry => !SubtitleQueueProgressStore.HasSavedState(entry.SaveKey)))
            .DistinctBy(entry => entry.SaveKey);

    private static bool SameMaterials(SubtitlePageModel left, SubtitlePageModel right) =>
        string.Equals(left.VideoFilePath, right.VideoFilePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.ScriptFilePath, right.ScriptFilePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.TranslateFilePath, right.TranslateFilePath, StringComparison.OrdinalIgnoreCase);

    private static void ShowError(string title, Exception exception)
    {
        Logger.Log($"{title}: {exception}", LogLevel.Error);
        SnackService.Show(title, exception.Message, ControlAppearance.Danger,
            new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
    }

    private sealed class PendingTasksDropHandler(SequentialTaskQueue<SubtitleQueueTaskModel> queue) : IDropTarget
    {
        public void DragOver(IDropInfo dropInfo)
        {
            var valid = dropInfo.Data is SubtitleQueueTaskModel task && queue.PendingTasks.Contains(task) &&
                        (dropInfo.TargetItem == null || dropInfo.TargetItem is SubtitleQueueTaskModel);
            dropInfo.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
            dropInfo.DropTargetAdorner = valid ? DropTargetAdorners.Insert : null;
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (queue.PendingTasks.Count < 2 || dropInfo.Data is not SubtitleQueueTaskModel task) return;
            var source = queue.PendingTasks.IndexOf(task);
            if (source < 0) return;
            var target = Math.Clamp(dropInfo.InsertIndex, 0, queue.PendingTasks.Count);
            if (source < target) target--;
            target = Math.Clamp(target, 0, queue.PendingTasks.Count - 1);
            if (source != target) queue.PendingTasks.Move(source, target);
        }
    }
}

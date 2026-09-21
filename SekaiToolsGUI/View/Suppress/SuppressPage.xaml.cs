using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.View.Suppress.Components;
using SekaiToolsGUI.ViewModel.Suppress;
using SekaiToolsMedia;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage<SuppressPageModel>
{
    private bool _preparingResources;

    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();
        GongDragDrop.SetDropHandler(PendingJobsList, new PendingJobsDropHandler(ViewModel.PendingJobs));

    }

    private static ISnackbarService SnackService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    public SuppressPageModel ViewModel => (SuppressPageModel)DataContext;

    public async void OnNavigatedTo()
    {
        if (ViewModel.ResourcesReady || _preparingResources) return;

        _preparingResources = true;
        ViewModel.BeginResourcePreparation();
        try
        {
            if (!await ResourceManager.Instance.CheckResource(ResourceType.VapourSynth))
                await EnsureVapourSynthResourceAsync();
            if (!await ResourceManager.Instance.CheckResource(ResourceType.VapourSynth))
                throw new InvalidDataException("VapourSynth 运行环境校验失败");

            ViewModel.CompleteResourcePreparation();
        }
        catch (Exception e)
        {
            ViewModel.FailResourcePreparation();
            (Application.Current.MainWindow as MainWindow)?.OnCheckResourceFailed(e, OnNavigatedTo);
        }
        finally
        {
            _preparingResources = false;
        }
    }

    private static async Task EnsureVapourSynthResourceAsync()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在准备 VapourSynth 运行环境，请稍候……");
        using var source = new CancellationTokenSource();
        var dialogTask = dialogService.ShowAsync(dialog, source.Token);
        try
        {
            await ResourceManager.Instance.EnsureResource(ResourceType.VapourSynth);
        }
        finally
        {
            await source.CancelAsync();
            try
            {
                await dialogTask;
            }
            catch (OperationCanceledException)
            {
                // 下载完成或失败时关闭等待对话框。
            }
        }
    }

    private async void CreateTask_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ResourcesReady) return;

        try
        {
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService
                                ?? throw new InvalidOperationException("内容对话框服务不可用");
            var dialog = new SuppressionTaskDialog(
                dialogService.GetDialogHostEx() ?? throw new InvalidOperationException("内容对话框宿主不可用"),
                !ViewModel.HasRunningJob);
            var result = await dialogService.ShowAsync(dialog, CancellationToken.None);
            if (result != ContentDialogResult.Primary) return;

            var overwriteExisting = dialog.ViewModel.OutputExists;
            if (overwriteExisting)
            {
                var overwriteResult = await dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "覆盖已有输出文件？",
                        Content = $"输出文件已存在：\n{dialog.ViewModel.OutputPath}\n\n继续执行将覆盖该文件。",
                        PrimaryButtonText = "继续覆盖",
                        CloseButtonText = "取消"
                    }, CancellationToken.None);
                if (overwriteResult != ContentDialogResult.Primary) return;
            }

            EnqueueSuppression(dialog.ViewModel.ToOptions(overwriteExisting), dialog.AutoStart);
        }
        catch (Exception exc)
        {
            Logger.Log($"创建压制任务失败: {exc}", LogLevel.Error);
            SnackService.Show("创建任务失败", exc.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.VideoClipOff24), TimeSpan.FromSeconds(6));
            if (Debugger.IsAttached) throw;
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCompleted();
        if (!ViewModel.HasRunningJob) ClearTaskbarProgress();
    }

    private void StartJobItem_OnStartRequested(object? sender, EventArgs e)
    {
        if (sender is SuppressionQueueJobItem { DataContext: SuppressionJobModel job })
            StartJob(job);
    }

    private void RemoveFinishedJob_OnRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is SuppressionFinishJobItem { DataContext: SuppressionJobModel job })
            ViewModel.CompletedJobs.Remove(job);
    }

    private sealed class PendingJobsDropHandler : IDropTarget
    {
        private readonly System.Collections.ObjectModel.ObservableCollection<SuppressionJobModel> _jobs;

        public PendingJobsDropHandler(
            System.Collections.ObjectModel.ObservableCollection<SuppressionJobModel> jobs)
        {
            _jobs = jobs;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is SuppressionJobModel job &&
                _jobs.Contains(job) &&
                (dropInfo.TargetItem == null || dropInfo.TargetItem is SuppressionJobModel))
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                return;
            }

            dropInfo.Effects = DragDropEffects.None;
            dropInfo.DropTargetAdorner = null;
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (_jobs.Count < 2 || dropInfo.Data is not SuppressionJobModel job) return;

            var sourceIndex = _jobs.IndexOf(job);
            if (sourceIndex < 0) return;

            var targetIndex = Math.Clamp(dropInfo.InsertIndex, 0, _jobs.Count);
            if (sourceIndex < targetIndex) targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, _jobs.Count - 1);

            if (sourceIndex != targetIndex)
                _jobs.Move(sourceIndex, targetIndex);
        }
    }

}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage<SuppressPageModel>
{
    private bool _preparingResources;

    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();

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

    private void PendingJobs_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SuppressionJobModel)) is not SuppressionJobModel source ||
            !ViewModel.PendingJobs.Contains(source)) return;

        var targetItem = FindVisualParent<SuppressionQueueJobItem>(e.OriginalSource as DependencyObject);
        var target = targetItem?.DataContext as SuppressionJobModel;
        var newIndex = target == null ? ViewModel.PendingJobs.Count : ViewModel.PendingJobs.IndexOf(target);
        if (target != null && targetItem != null && e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2) newIndex++;

        var oldIndex = ViewModel.PendingJobs.IndexOf(source);
        if (oldIndex < 0) return;
        if (oldIndex < newIndex) newIndex--;
        if (oldIndex == newIndex) return;
        ViewModel.PendingJobs.Move(oldIndex, Math.Clamp(newIndex, 0, ViewModel.PendingJobs.Count - 1));
        e.Handled = true;
    }

    private void PendingJobs_OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SuppressionJobModel)) is SuppressionJobModel job &&
            ViewModel.PendingJobs.Contains(job))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T result) return result;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }

}

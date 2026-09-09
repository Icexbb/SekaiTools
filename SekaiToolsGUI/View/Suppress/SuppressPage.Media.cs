using System.Windows;
using System.Windows.Shell;
using SekaiToolsGUI.Service;
using SekaiToolsMedia;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage
{
    private static readonly VideoSuppressionQueue Queue = new(async (options, report, token) =>
    {
        using var powerRequest = SystemPowerRequest.Acquire("SekaiTools 正在压制视频");
        using var suppressor = new VideoSuppressor(ResourceManager.Instance);
        suppressor.ProgressChanged += report;
        await suppressor.SuppressAsync(options, token);
    });

    private void EnqueueSuppression(VideoSuppressionOptions options)
    {
        options.Validate();
        var output = System.IO.Path.GetFullPath(options.OutputPath);
        if (ViewModel.Jobs.Any(x => (x.CanCancel || x.Progress.State == VideoSuppressionState.Cancelling) &&
                string.Equals(System.IO.Path.GetFullPath(x.OutputPath), output, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("队列中已有任务使用此输出路径，请选择其他路径。");

        var job = new VideoSuppressionJob(options);
        var model = new SekaiToolsGUI.ViewModel.Suppress.SuppressionJobModel(job);
        job.ProgressChanged += progress =>
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(() =>
            {
                model.Progress = progress;
                ApplyTaskbarProgress(progress);
            });
        };
        Queue.Enqueue(job);
        ViewModel.Jobs.Add(model);
    }

    private static void ApplyTaskbarProgress(VideoSuppressionProgress progress)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow) return;
        switch (progress.State)
        {
            case VideoSuppressionState.Preparing:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Indeterminate, 0);
                break;
            case VideoSuppressionState.Running:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Normal, progress.Fraction);
                break;
            case VideoSuppressionState.Cancelling:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Paused, progress.Fraction);
                break;
            case VideoSuppressionState.Completed:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Normal, 1);
                break;
            case VideoSuppressionState.Failed:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Error, progress.Fraction);
                break;
            default:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.None, 0);
                break;
        }
    }

    private static void ClearTaskbarProgress()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.None, 0);
    }

    internal static void DisposeSuppressor()
    {
        Queue.Dispose();
        Queue.Completion.GetAwaiter().GetResult();
    }
}

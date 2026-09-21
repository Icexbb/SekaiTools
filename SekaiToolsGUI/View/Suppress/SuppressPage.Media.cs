using System.Windows;
using System.Windows.Shell;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.ViewModel.Suppress;
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

    private void EnqueueSuppression(VideoSuppressionOptions options, bool autoStart)
    {
        options.Validate();
        var output = System.IO.Path.GetFullPath(options.OutputPath);
        if (ViewModel.PendingJobs.Any(x =>
                string.Equals(System.IO.Path.GetFullPath(x.OutputPath), output, StringComparison.OrdinalIgnoreCase)) ||
            (ViewModel.RunningJob != null &&
             string.Equals(System.IO.Path.GetFullPath(ViewModel.RunningJob.OutputPath), output, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("队列中已有任务使用此输出路径，请选择其他路径。");

        var job = new VideoSuppressionJob(options);
        var model = new SuppressionJobModel(job);
        job.ProgressChanged += progress =>
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(() =>
            {
                var wasRunning = ReferenceEquals(ViewModel.RunningJob, model);
                model.Progress = progress;
                ApplyTaskbarProgress(progress);
                if (wasRunning && model.IsFinished) StartNextQueuedJob();
            });
        };
        ViewModel.AddPending(model);
        if (autoStart) StartJob(model);
    }

    internal void StartJob(SuppressionJobModel model)
    {
        if (!ViewModel.TryStart(model)) return;

        try
        {
            Queue.Enqueue(model.Job);
        }
        catch (Exception ex)
        {
            model.Progress = model.Progress with
            {
                State = VideoSuppressionState.Failed,
                Log = $"压制失败：{ex.Message}"
            };
        }
    }

    private void StartNextQueuedJob()
    {
        if (!ViewModel.AutoRunQueue || ViewModel.HasRunningJob) return;
        var next = ViewModel.PendingJobs.FirstOrDefault();
        if (next != null) StartJob(next);
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

    internal static Task DisposeSuppressorAsync()
    {
        return Queue.DisposeAsync(TimeSpan.FromSeconds(8));
    }
}

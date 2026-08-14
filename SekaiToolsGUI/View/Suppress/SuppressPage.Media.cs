using System.IO;
using System.Windows;
using System.Windows.Shell;
using SekaiToolsGUI.Service;
using SekaiToolsMedia;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage
{
    private static readonly VideoSuppressor VideoSuppressor = new(ResourceManager.Instance);
    private IReadOnlyList<VideoSuppressionQueueItem> _activeQueueItems = [];
    private int _activeQueueIndex;

    private void InitializeSuppressor()
    {
        VideoSuppressor.ProgressChanged += ApplySuppressionProgress;
    }

    private async Task BeginSuppressQueueAsync(IReadOnlySet<string> confirmedOverwritePaths)
    {
        ViewModel.BeginTask();
        var encodingSettings = new X264EncodingSettings(
            ViewModel.QualityPreset,
            ViewModel.SpeedPreset,
            ViewModel.SuppressCrf);

        using var powerRequest = SystemPowerRequest.Acquire("SekaiTools 正在压制视频");
        _activeQueueItems = ViewModel.QueueItems.ToArray();
        try
        {
            for (_activeQueueIndex = 0; _activeQueueIndex < _activeQueueItems.Count; _activeQueueIndex++)
            {
                var item = _activeQueueItems[_activeQueueIndex];
                var options = new VideoSuppressionOptions(
                    item.SourceVideo,
                    item.SourceSubtitle,
                    item.OutputPath,
                    encodingSettings,
                    OverwriteExisting: confirmedOverwritePaths.Contains(Path.GetFullPath(item.OutputPath)));
                await VideoSuppressor.SuppressAsync(options);
            }
        }
        finally
        {
            _activeQueueItems = [];
            _activeQueueIndex = 0;
        }
    }

    private async Task CancelSuppressAsync()
    {
        await VideoSuppressor.CancelAsync();
    }

    private void ApplySuppressionProgress(VideoSuppressionProgress progress)
    {
        var activeQueueItems = _activeQueueItems;
        var activeQueueIndex = _activeQueueIndex;

        void Apply()
        {
            if (activeQueueItems.Count > 1)
            {
                var item = activeQueueItems[Math.Min(activeQueueIndex, activeQueueItems.Count - 1)];
                ViewModel.ApplyQueueProgress(
                    progress, activeQueueIndex, activeQueueItems.Count, item.DisplayName);
            }
            else
            {
                ViewModel.ApplyProgress(progress);
            }

            ApplyTaskbarProgress(ViewModel.TaskState, ViewModel.Progression);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    private static void ApplyTaskbarProgress(VideoSuppressionState state, double fraction)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow) return;
        switch (state)
        {
            case VideoSuppressionState.Preparing:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Indeterminate, 0);
                break;
            case VideoSuppressionState.Running:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Normal, fraction);
                break;
            case VideoSuppressionState.Cancelling:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Paused, fraction);
                break;
            case VideoSuppressionState.Completed:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Normal, 1);
                break;
            case VideoSuppressionState.Failed:
                mainWindow.SetTaskbarProgressState(TaskbarItemProgressState.Error, fraction);
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
        VideoSuppressor.Dispose();
    }

}

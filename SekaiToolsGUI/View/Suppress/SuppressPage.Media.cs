using System.Windows;
using System.Windows.Shell;
using SekaiToolsGUI.Service;
using SekaiToolsMedia;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage
{
    private static readonly VideoSuppressor VideoSuppressor = new(ResourceManager.Instance);

    private void InitializeSuppressor()
    {
        VideoSuppressor.ProgressChanged += ApplySuppressionProgress;
    }

    private async Task BeginSuppressAsync(bool overwriteExisting)
    {
        ViewModel.BeginTask();
        var x264Parameters = ViewModel.UseComplexConfig
            ? X264Params.Instance.GetX264Params()
            : X264Params.Instance.GetSimpleX264Params();
        var options = new VideoSuppressionOptions(
            ViewModel.SourceVideo,
            ViewModel.SourceSubtitle,
            ViewModel.OutputPath,
            x264Parameters,
            ViewModel.SourceFrameCount,
            overwriteExisting);

        using var powerRequest = SystemPowerRequest.Acquire("SekaiTools 正在压制视频");
        await VideoSuppressor.SuppressAsync(options);
    }

    private async Task CancelSuppressAsync()
    {
        await VideoSuppressor.CancelAsync();
    }

    private void ApplySuppressionProgress(VideoSuppressionProgress progress)
    {
        void Apply()
        {
            ViewModel.ApplyProgress(progress);
            ApplyTaskbarProgress(progress);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
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
        VideoSuppressor.Dispose();
    }

}

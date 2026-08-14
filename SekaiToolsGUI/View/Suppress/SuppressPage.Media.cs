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
        ViewModel.ReloadStatus();
        ViewModel.HasNotStarted = false;
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
        ViewModel.ReloadStatus();
    }

    private void ApplySuppressionProgress(VideoSuppressionProgress progress)
    {
        void Apply()
        {
            ViewModel.SourceFrameCount = progress.TotalFrames;
            ViewModel.Progression = progress.Fraction;
            ViewModel.Fps = progress.FramesPerSecond;
            ViewModel.Running = progress.Running;
            ViewModel.Status = progress.Status;
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }
    internal static void DisposeSuppressor()
    {
        VideoSuppressor.Dispose();
    }

}

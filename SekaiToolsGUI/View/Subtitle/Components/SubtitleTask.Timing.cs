using System.IO;
using SekaiToolsBase;
using SekaiToolsCore.Timing;
using SekaiToolsGUI.Service;
using SekaiToolsInfrastructure.Resources;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTask
{
    private CancellationTokenSource? _timingCancellation;
    private VideoTimingResult? _timingResult;

    private async Task RunTimingOnlyAsync()
    {
        if (!File.Exists(ViewModel.VideoFilePath)) throw new FileNotFoundException("视频文件不存在");
        using var source = new CancellationTokenSource();
        using var power = SystemPowerRequest.Acquire("SekaiTools 正在识别纯视频时间轴");
        _timingCancellation = source;
        ViewModel.HasNotStarted = false;
        ViewModel.IsRunning = true;
        try
        {
            var path = ResourceManager.Instance.GetVideoProcessResourcePath("menu-107px.png");
            var videoPath = ViewModel.VideoFilePath;
            _timingResult = await Task.Run(() => new VideoTimingRecognizer().Recognize(videoPath,
                path, LinePanel_AddDialogLine, OnProgressChanged, source.Token));
            await _resultQueue.FlushAsync();
            if (!_timingResult.Canceled && _timingResult.Dialogs.Count == 0)
                throw new InvalidDataException("未检测到对白。当前实验模式仅支持常规游戏对话框，请检查视频画面布局。");
            ViewModel.DialogTotal = _timingResult.Dialogs.Count;
            ViewModel.IsCanceled = _timingResult.Canceled;
            ViewModel.IsFinished = !_timingResult.Canceled;
            ViewModel.Progress = _timingResult.Canceled ? ViewModel.Progress : 1;
            ProcessView.ProgressBarProgression.Value = ViewModel.Progress;
            ProcessView.ProgressBarProgression.Maximum = 1;
            ProcessView.TextBlockProgression.Text = $"{ViewModel.Progress:P}";
            SetTimelineVideoDuration();
            Logger.Log($"纯视频任务结束: {_timingResult.Dialogs.Count} 条候选，已取消={_timingResult.Canceled}");
        }
        finally
        {
            _timingCancellation = null;
            ViewModel.IsRunning = false;
            ViewModel.IsCanceling = false;
        }
    }
}

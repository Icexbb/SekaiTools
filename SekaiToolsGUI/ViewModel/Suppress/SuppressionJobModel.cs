using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressionJobModel(VideoSuppressionJob job) : ViewModelBase
{
    public VideoSuppressionJob Job { get; } = job;
    public string SourceVideo => Job.Options.SourceVideo;
    public string OutputPath => Job.Options.OutputPath;
    public string FileName => System.IO.Path.GetFileName(SourceVideo);
    public string EncodingDescription => $"{(string.IsNullOrWhiteSpace(Job.Options.SourceSubtitle) ? "仅转码" : "烧录字幕（不可关闭）")} · CRF {Job.Options.EncodingSettings.Crf} · {Job.Options.EncodingSettings.FfmpegPreset}";
    public Wpf.Ui.Controls.SymbolRegular StatusIcon => Progress.State switch
    {
        VideoSuppressionState.Completed => Wpf.Ui.Controls.SymbolRegular.Checkmark24,
        VideoSuppressionState.Failed => Wpf.Ui.Controls.SymbolRegular.ErrorCircle24,
        _ => Wpf.Ui.Controls.SymbolRegular.Dismiss24
    };
    public string FailureSummary => Progress.State == VideoSuppressionState.Failed
        ? Progress.Log.Split('\n').LastOrDefault(line => line.Contains("压制失败"))?.Trim() ?? "任务失败，请查看日志。" : "";
    public bool IsFinished => Progress.State is VideoSuppressionState.Completed or VideoSuppressionState.Cancelled or VideoSuppressionState.Failed;
    public bool IsPreparing => Progress.State == VideoSuppressionState.Preparing;
    public bool IsEditing
    {
        get => GetProperty(false);
        internal set { SetProperty(value); OnPropertyChanged(nameof(WaitingStatus)); }
    }
    public string WaitingStatus => IsEditing ? "正在编辑" : "等待中";
    public bool CanStart
    {
        get => GetProperty(false);
        internal set => SetProperty(value);
    }

    public VideoSuppressionProgress Progress
    {
        get => GetProperty(Job.Progress);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsFinished));
            OnPropertyChanged(nameof(IsPreparing));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(FailureSummary));
        }
    }
    public bool CanCancel => Progress.State is VideoSuppressionState.Idle or VideoSuppressionState.Preparing or VideoSuppressionState.Running;
    public bool IsCompleted => Progress.State == VideoSuppressionState.Completed;
    public void Cancel()
    {
        if (!CanCancel) return;
        Progress = Progress with
        {
            State = Progress.State == VideoSuppressionState.Idle ? VideoSuppressionState.Cancelled : VideoSuppressionState.Cancelling,
        };
        Job.Cancel();
    }
}

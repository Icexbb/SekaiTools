using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressionJobModel(VideoSuppressionJob job) : ViewModelBase
{
    public VideoSuppressionJob Job { get; } = job;
    public string SourceVideo => Job.Options.SourceVideo;
    public string FfmpegCommandDisplay => string.IsNullOrWhiteSpace(Progress.FfmpegCommand) ? "任务开始后显示实际 FFmpeg 参数（包含音轨检测结果与临时输出路径）。" : Progress.FfmpegCommand;
    public string EditLabel => IsWaiting ? "编辑任务" : "修改后重试";
    public bool CanEdit => !IsActive && !IsEditing;
    public string SubtitleDescription => string.IsNullOrWhiteSpace(Job.Options.SourceSubtitle) ? "未选择字幕，仅转码" : Job.Options.SourceSubtitle;
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
        internal set { SetProperty(value); OnPropertyChanged(nameof(WaitingStatus)); OnPropertyChanged(nameof(ListStatus)); OnPropertyChanged(nameof(CanReorder)); OnPropertyChanged(nameof(CanEdit)); }
    }
    public bool IsFailed => Progress.State == VideoSuppressionState.Failed;
    public bool IsSelected { get => GetProperty(false); set => SetProperty(value); }
    public bool CanReorder => Progress.State == VideoSuppressionState.Idle && !IsEditing;
    public bool IsActive => Progress.Running;
    public bool IsWaiting => Progress.State == VideoSuppressionState.Idle;
    public string ListStatus => IsEditing ? "正在编辑" : Progress.Status;
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
            OnPropertyChanged(nameof(FfmpegCommandDisplay));
            OnPropertyChanged(nameof(EditLabel));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(ListStatus));
            OnPropertyChanged(nameof(CanReorder));
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

using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressionJobModel(VideoSuppressionJob job) : ViewModelBase
{
    public VideoSuppressionJob Job { get; } = job;
    public string SourceVideo => Job.Options.SourceVideo;
    public string OutputPath => Job.Options.OutputPath;
    public string FileName => System.IO.Path.GetFileName(SourceVideo);
    public string EncodingDescription => $"{(string.IsNullOrWhiteSpace(Job.Options.SourceSubtitle) ? "仅转码" : "内嵌字幕")} · CRF {Job.Options.EncodingSettings.Crf} · {Job.Options.EncodingSettings.FfmpegPreset}";
    public bool IsFinished => Progress.State is VideoSuppressionState.Completed or VideoSuppressionState.Cancelled or VideoSuppressionState.Failed;
    public bool IsPreparing => Progress.State == VideoSuppressionState.Preparing;
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

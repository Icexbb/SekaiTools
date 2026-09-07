using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressionJobModel(VideoSuppressionJob job) : ViewModelBase
{
    public VideoSuppressionJob Job { get; } = job;
    public string SourceVideo => Job.Options.SourceVideo;
    public string OutputPath => Job.Options.OutputPath;
    public VideoSuppressionProgress Progress
    {
        get => GetProperty(Job.Progress);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(IsCompleted));
        }
    }
    public bool CanCancel => Progress.State is VideoSuppressionState.Idle or VideoSuppressionState.Preparing or VideoSuppressionState.Running;
    public bool IsCompleted => Progress.State == VideoSuppressionState.Completed;
    public void Cancel()
    {
        Progress = Progress with
        {
            State = Progress.State == VideoSuppressionState.Idle ? VideoSuppressionState.Cancelled : VideoSuppressionState.Cancelling,
            Status = Progress.State == VideoSuppressionState.Idle ? "任务已取消" : "正在取消…"
        };
        Job.Cancel();
    }
}

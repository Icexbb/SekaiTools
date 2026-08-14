using System.Collections.ObjectModel;
using System.IO;
using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public class SuppressPageModel : ViewModelBase
{
    private bool _synchronizingQueue;

    public static SuppressPageModel Instance { get; } = new();

    public ObservableCollection<VideoSuppressionQueueItem> QueueItems { get; } = [];

    public string SourceVideo
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    public int SourceFrameCount
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public string SourceSubtitle
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            if (!_synchronizingQueue && QueueItems.Count == 1)
                QueueItems[0] = QueueItems[0] with { SourceSubtitle = value };
            UpdateConfigStatus();
        }
    }

    public string OutputPath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            if (!_synchronizingQueue && QueueItems.Count == 1)
                QueueItems[0] = QueueItems[0] with { OutputPath = value };
            UpdateConfigStatus();
        }
    }

    public int QueueCount => QueueItems.Count;

    public bool HasSourceVideos => QueueItems.Count > 0;

    public bool IsSingleVideo => QueueItems.Count == 1;

    public bool IsBatchQueue => QueueItems.Count > 1;

    public string QueueSummary => QueueItems.Count switch
    {
        0 => "尚未选择视频，可将多个视频拖放到此页面",
        1 => QueueItems[0].SourceVideo,
        _ => $"已选择 {QueueItems.Count} 个视频"
    };

    public void SetSourceVideos(IEnumerable<string> sourceVideos)
    {
        QueueItems.Clear();
        AddSourceVideosCore(sourceVideos);
        SynchronizePrimaryItem();
    }

    public void AddSourceVideos(IEnumerable<string> sourceVideos)
    {
        AddSourceVideosCore(sourceVideos);
        SynchronizePrimaryItem();
    }

    public void RemoveQueueItem(VideoSuppressionQueueItem item)
    {
        QueueItems.Remove(item);
        SynchronizePrimaryItem();
    }

    public void ClearQueue()
    {
        QueueItems.Clear();
        SynchronizePrimaryItem();
    }

    private void AddSourceVideosCore(IEnumerable<string> sourceVideos)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var existingPaths = QueueItems.Select(item => item.SourceVideo).ToHashSet(comparer);
        foreach (var sourceVideo in sourceVideos.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var item = VideoSuppressionQueueItem.Create(sourceVideo);
            if (existingPaths.Add(item.SourceVideo)) QueueItems.Add(item);
        }
    }

    private void SynchronizePrimaryItem()
    {
        _synchronizingQueue = true;
        try
        {
            var firstItem = QueueItems.FirstOrDefault();
            SourceVideo = firstItem?.SourceVideo ?? "";
            SourceSubtitle = firstItem?.SourceSubtitle ?? "";
            OutputPath = firstItem?.OutputPath ?? "";
            SourceFrameCount = 0;
        }
        finally
        {
            _synchronizingQueue = false;
        }

        OnPropertyChanged(nameof(QueueCount));
        OnPropertyChanged(nameof(HasSourceVideos));
        OnPropertyChanged(nameof(IsSingleVideo));
        OnPropertyChanged(nameof(IsBatchQueue));
        OnPropertyChanged(nameof(QueueSummary));
        UpdateConfigStatus();
    }

    private bool GetCanStartSuppress => ResourcesReady &&
                                         TaskState == VideoSuppressionState.Idle &&
                                         string.IsNullOrEmpty(GetQueueError());

    private string GetConfigError
    {
        get
        {
            if (IsPreparingResources) return "正在准备视频压制环境，请稍候";
            if (!ResourcesReady) return string.IsNullOrWhiteSpace(ResourcePreparationError)
                ? "视频压制环境尚未就绪"
                : ResourcePreparationError;
            return GetQueueError();
        }
    }

    private string GetQueueError()
    {
        if (QueueItems.Count == 0) return "请选择一个或多个视频文件";
        foreach (var item in QueueItems)
        {
            if (!File.Exists(item.SourceVideo)) return $"视频文件不存在：{item.DisplayName}";
            if (!string.IsNullOrWhiteSpace(item.SourceSubtitle) && !File.Exists(item.SourceSubtitle))
                return $"字幕文件不存在：{Path.GetFileName(item.SourceSubtitle)}";
            if (string.IsNullOrWhiteSpace(item.OutputPath)) return $"请选择 {item.DisplayName} 的输出路径";
            if (PathsEqual(item.SourceVideo, item.OutputPath))
                return $"{item.DisplayName} 的输出路径不能与源视频相同";
        }

        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (QueueItems.Select(item => Path.GetFullPath(item.OutputPath)).Distinct(pathComparer).Count() !=
            QueueItems.Count)
            return "队列中存在重复的输出路径，请移除同名视频";
        return "";
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    public bool CanStartSuppress
    {
        get => GetProperty(GetCanStartSuppress);
        set => SetProperty(value);
    }

    public string ConfigError
    {
        get => GetProperty(GetConfigError);
        private set => SetProperty(value);
    }

    public bool IsPreparingResources
    {
        get => GetProperty(false);
        private set => SetProperty(value);
    }

    public bool ResourcesReady
    {
        get => GetProperty(false);
        private set => SetProperty(value);
    }

    public string ResourcePreparationError
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }


    public VideoSuppressionState TaskState
    {
        get => GetProperty(VideoSuppressionState.Idle);
        private set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasNotStarted));
            OnPropertyChanged(nameof(Running));
            OnPropertyChanged(nameof(IsTaskActive));
            OnPropertyChanged(nameof(CanControlTask));
            OnPropertyChanged(nameof(CanClearTask));
            OnPropertyChanged(nameof(ShowResult));
            OnPropertyChanged(nameof(TaskControlText));
            OnPropertyChanged(nameof(ProgressDescription));
        }
    }

    public bool HasNotStarted => TaskState == VideoSuppressionState.Idle;

    public bool Running => TaskState is VideoSuppressionState.Preparing
        or VideoSuppressionState.Running
        or VideoSuppressionState.Cancelling;

    public bool IsTaskActive => TaskState is VideoSuppressionState.Preparing or VideoSuppressionState.Running;

    public bool CanControlTask => TaskState != VideoSuppressionState.Cancelling;

    public bool CanClearTask => !Running;

    public bool ShowResult => TaskState == VideoSuppressionState.Completed;

    public string TaskControlText => TaskState switch
    {
        VideoSuppressionState.Preparing or VideoSuppressionState.Running => "取消压制",
        VideoSuppressionState.Cancelling => "正在取消…",
        _ => "返回设置"
    };

    public int SuppressCrf
    {
        get => GetProperty(21);
        set => SetProperty(value);
    }

    public VideoQualityPreset QualityPreset
    {
        get => GetProperty(VideoQualityPreset.Balanced);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(UseCustomCrf));
        }
    }

    public VideoEncodingSpeedPreset SpeedPreset
    {
        get => GetProperty(VideoEncodingSpeedPreset.Balanced);
        set => SetProperty(value);
    }

    public bool UseCustomCrf => QualityPreset == VideoQualityPreset.Custom;


    public double Progression
    {
        get => GetProperty(0.0);
        set => SetProperty(value);
    }

    public double Fps
    {
        get => GetProperty(0.0);
        set => SetProperty(value);
    }

    public string Status
    {
        get => GetProperty("");
        set => SetProperty(value.Trim());
    }

    public string DetailLog
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string Speed
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public TimeSpan Elapsed
    {
        get => GetProperty(TimeSpan.Zero);
        set => SetProperty(value);
    }

    public TimeSpan? EstimatedRemaining
    {
        get => GetProperty<TimeSpan?>();
        set => SetProperty(value);
    }

    public string ProgressDescription
    {
        get
        {
            var elapsedLabel = IsBatchQueue ? "当前已用" : "已用";
            if (TaskState == VideoSuppressionState.Preparing)
                return $"{elapsedLabel} {FormatDuration(Elapsed)}";
            if (TaskState == VideoSuppressionState.Idle) return "";

            var parts = new List<string> { $"{Progression:P1}" };
            if (Fps > 0) parts.Add($"{Fps:F1} FPS");
            if (!string.IsNullOrWhiteSpace(Speed) && Speed != "N/A") parts.Add(Speed);
            parts.Add($"{elapsedLabel} {FormatDuration(Elapsed)}");
            if (EstimatedRemaining is { } remaining)
                parts.Add($"{(IsBatchQueue ? "当前预计剩余" : "预计剩余")} {FormatDuration(remaining)}");
            return string.Join(" · ", parts);
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void UpdateConfigStatus()
    {
        CanStartSuppress = GetCanStartSuppress;
        ConfigError = GetConfigError;
    }

    public void BeginResourcePreparation()
    {
        IsPreparingResources = true;
        ResourcesReady = false;
        ResourcePreparationError = "";
        UpdateConfigStatus();
    }

    public void CompleteResourcePreparation()
    {
        IsPreparingResources = false;
        ResourcesReady = true;
        ResourcePreparationError = "";
        UpdateConfigStatus();
    }

    public void FailResourcePreparation()
    {
        IsPreparingResources = false;
        ResourcesReady = false;
        ResourcePreparationError = "视频压制环境准备失败，请重试或检查设置";
        UpdateConfigStatus();
    }

    public void BeginTask()
    {
        Status = "正在准备压制任务…";
        DetailLog = "";
        Progression = 0;
        Fps = 0;
        Speed = "";
        Elapsed = TimeSpan.Zero;
        EstimatedRemaining = null;
        TaskState = VideoSuppressionState.Preparing;
        OnPropertyChanged(nameof(ProgressDescription));
    }

    public void ApplyProgress(VideoSuppressionProgress progress)
    {
        SourceFrameCount = progress.TotalFrames;
        Progression = progress.Fraction;
        Fps = progress.FramesPerSecond;
        Status = progress.Status;
        DetailLog = progress.DetailLog;
        Speed = progress.Speed;
        Elapsed = progress.Elapsed;
        EstimatedRemaining = progress.EstimatedRemaining;
        TaskState = progress.State;
        OnPropertyChanged(nameof(ProgressDescription));
    }

    public void ApplyQueueProgress(
        VideoSuppressionProgress progress,
        int itemIndex,
        int itemCount,
        string displayName)
    {
        var isLastItem = itemIndex == itemCount - 1;
        var state = progress.State switch
        {
            VideoSuppressionState.Completed when !isLastItem => VideoSuppressionState.Running,
            VideoSuppressionState.Preparing when itemIndex > 0 => VideoSuppressionState.Running,
            _ => progress.State
        };

        SourceFrameCount = progress.TotalFrames;
        Progression = Math.Clamp((itemIndex + progress.Fraction) / itemCount, 0, 1);
        Fps = progress.FramesPerSecond;
        Status = $"[{itemIndex + 1}/{itemCount}] {displayName}\n{progress.Status}";
        DetailLog = progress.DetailLog;
        Speed = progress.Speed;
        Elapsed = progress.Elapsed;
        EstimatedRemaining = progress.EstimatedRemaining;
        TaskState = state;
        OnPropertyChanged(nameof(ProgressDescription));
    }

    public void FailTask(string message)
    {
        Status = string.IsNullOrWhiteSpace(Status) ? message : $"{Status}\n{message}";
        TaskState = VideoSuppressionState.Failed;
    }

    public void ReloadStatus()
    {
        Status = "";
        DetailLog = "";
        Progression = 0;
        Fps = 0;
        Speed = "";
        Elapsed = TimeSpan.Zero;
        EstimatedRemaining = null;
        TaskState = VideoSuppressionState.Idle;
        OnPropertyChanged(nameof(ProgressDescription));
        UpdateConfigStatus();
    }

    public void Reset()
    {
        ReloadStatus();
        ClearQueue();
        SuppressCrf = 21;
        QualityPreset = VideoQualityPreset.Balanced;
        SpeedPreset = VideoEncodingSpeedPreset.Balanced;
    }
}

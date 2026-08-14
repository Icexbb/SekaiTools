using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public class SuppressPageModel : ViewModelBase
{
    public static SuppressPageModel Instance { get; } = new();

    public string SourceVideo
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SourceFrameCount = 0;
            SourceSubtitle = "";
            if (File.Exists(value))
            {
                using var capture = new VideoCapture(value);
                SourceFrameCount = (int)capture.Get(CapProp.FrameCount);

                var guess = Path.ChangeExtension(value, ".ass");
                if (File.Exists(guess)) SourceSubtitle = guess;
            }

            OutputPath = Path.Join(Path.GetDirectoryName(value),
                "[STVS]" + Path.GetFileNameWithoutExtension(value) + ".mp4");
            UpdateConfigStatus();
        }
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
            UpdateConfigStatus();
        }
    }

    public string OutputPath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            UpdateConfigStatus();
        }
    }

    private bool GetCanStartSuppress => File.Exists(SourceVideo) &&
                                         (string.IsNullOrWhiteSpace(SourceSubtitle) || File.Exists(SourceSubtitle)) &&
                                         !string.IsNullOrWhiteSpace(OutputPath) &&
                                         !OutputMatchesSource &&
                                         ResourcesReady &&
                                         TaskState == VideoSuppressionState.Idle;

    private bool OutputMatchesSource => PathsEqual(SourceVideo, OutputPath);

    private string GetConfigError
    {
        get
        {
            if (IsPreparingResources) return "正在准备视频压制环境，请稍候";
            if (!ResourcesReady) return string.IsNullOrWhiteSpace(ResourcePreparationError)
                ? "视频压制环境尚未就绪"
                : ResourcePreparationError;
            if (string.IsNullOrWhiteSpace(SourceVideo)) return "请选择视频文件";
            if (!File.Exists(SourceVideo)) return "视频文件不存在，请重新选择";
            if (!string.IsNullOrWhiteSpace(SourceSubtitle) && !File.Exists(SourceSubtitle))
                return "字幕文件不存在，请重新选择或清除";
            if (string.IsNullOrWhiteSpace(OutputPath)) return "请选择输出路径";
            if (OutputMatchesSource) return "输出路径不能与源视频相同";
            return "";
        }
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
        set
        {
            SetProperty(value);
            X264Params.Instance.Crf = value;
        }
    }

    public bool UseComplexConfig
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }


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
            if (TaskState == VideoSuppressionState.Preparing)
                return $"已用 {FormatDuration(Elapsed)}";
            if (TaskState == VideoSuppressionState.Idle) return "";

            var parts = new List<string> { $"{Progression:P1}" };
            if (Fps > 0) parts.Add($"{Fps:F1} FPS");
            if (!string.IsNullOrWhiteSpace(Speed) && Speed != "N/A") parts.Add(Speed);
            parts.Add($"已用 {FormatDuration(Elapsed)}");
            if (EstimatedRemaining is { } remaining)
                parts.Add($"预计剩余 {FormatDuration(remaining)}");
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
        SourceVideo = "";
        SourceSubtitle = "";
        OutputPath = "";
        SuppressCrf = 21;
    }
}

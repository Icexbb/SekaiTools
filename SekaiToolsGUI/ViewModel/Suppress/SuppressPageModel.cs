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

    public int ProcessedFrames
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public string Bitrate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string Speed
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string OutputSize
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string OutputTime
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string ProgressDescription
    {
        get
        {
            var framePosition = SourceFrameCount > 0
                ? $"帧位 {ProcessedFrames:N0} / {SourceFrameCount:N0}"
                : $"帧位 {ProcessedFrames:N0}";
            var parts = new List<string>
            {
                framePosition,
                Fps > 0 ? $"{Fps:F1} FPS" : "FPS —",
                $"进度 {Progression:P1}"
            };
            // if (!string.IsNullOrWhiteSpace(Bitrate)) parts.Add($"码率 {Bitrate}");
            if (!string.IsNullOrWhiteSpace(Speed)) parts.Add($"速度 {Speed}");
            if (!string.IsNullOrWhiteSpace(OutputTime)) parts.Add($"时间 {OutputTime}");
            // if (!string.IsNullOrWhiteSpace(OutputSize)) parts.Add($"大小 {OutputSize}");
            return string.Join(" · ", parts);
        }
    }

    public string Status
    {
        get => GetProperty("");
        set => SetProperty(value.Trim());
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
        Progression = 0;
        Fps = 0;
        ProcessedFrames = 0;
        Bitrate = "";
        Speed = "";
        OutputSize = "";
        OutputTime = "";
        TaskState = VideoSuppressionState.Preparing;
        OnPropertyChanged(nameof(ProgressDescription));
    }

    public void ApplyProgress(VideoSuppressionProgress progress)
    {
        SourceFrameCount = progress.TotalFrames;
        Progression = progress.Fraction;
        Fps = progress.FramesPerSecond;
        ProcessedFrames = progress.ProcessedFrames;
        Bitrate = progress.Bitrate;
        Speed = progress.Speed;
        OutputSize = progress.OutputSize;
        OutputTime = progress.OutputTime;
        Status = progress.Status;
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
        Progression = 0;
        Fps = 0;
        ProcessedFrames = 0;
        Bitrate = "";
        Speed = "";
        OutputSize = "";
        OutputTime = "";
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
        QualityPreset = VideoQualityPreset.Balanced;
        SpeedPreset = VideoEncodingSpeedPreset.Balanced;
    }
}

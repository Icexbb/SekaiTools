using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressTaskSettingsModel : ViewModelBase
{
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
        private set => SetProperty(value);
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

    public bool OutputExists => File.Exists(OutputPath);
    public string SubmissionError
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public bool CanSubmit => File.Exists(SourceVideo)
                             && (string.IsNullOrWhiteSpace(SourceSubtitle) || File.Exists(SourceSubtitle))
                             && !string.IsNullOrWhiteSpace(OutputPath)
                             && Directory.Exists(Path.GetDirectoryName(OutputPath))
                             && !PathsEqual(SourceVideo, OutputPath);

    public string ConfigError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SourceVideo)) return "请选择视频文件";
            if (!File.Exists(SourceVideo)) return "视频文件不存在，请重新选择";
            if (!string.IsNullOrWhiteSpace(SourceSubtitle) && !File.Exists(SourceSubtitle))
                return "字幕文件不存在，请重新选择或清除";
            if (string.IsNullOrWhiteSpace(OutputPath)) return "请选择输出路径";
            if (!Directory.Exists(Path.GetDirectoryName(OutputPath))) return "输出目录不存在，请重新选择";
            if (PathsEqual(SourceVideo, OutputPath)) return "输出路径不能与源视频相同";
            return "";
        }
    }

    public VideoSuppressionOptions ToOptions(bool overwriteExisting = false) => new(
        SourceVideo,
        SourceSubtitle,
        OutputPath,
        new X264EncodingSettings(QualityPreset, SpeedPreset, SuppressCrf),
        SourceFrameCount, overwriteExisting);

    private void UpdateConfigStatus()
    {
        SubmissionError = "";
        OnPropertyChanged(nameof(OutputExists));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfigError));
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }
}

using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public sealed class SuppressTaskSettingsModel : ViewModelBase
{
    private int _analysisVersion;
    public SuppressTaskSettingsModel()
    {
        var settings = SekaiToolsGUI.Service.SuppressionPreferences.Load();
        QualityPreset = settings.Quality;
        SpeedPreset = settings.Speed;
        SuppressCrf = settings.CustomCrf;
        EncodingThreads = settings.Threads;
    }

    public bool IsAnalyzing { get => GetProperty(false); private set { SetProperty(value); } }
    public string MediaDescription { get => GetProperty("请选择视频，或将视频与 ASS 字幕拖入此处"); private set { SetProperty(value); } }
    public string AnalysisError { get => GetProperty(""); private set { SetProperty(value); } }
    public string SubtitleNotice { get => GetProperty(""); set { SetProperty(value); } }
    public string TaskSummary => $"{(string.IsNullOrWhiteSpace(SourceSubtitle) ? "仅转码" : "烧录字幕（不可关闭）")} · MP4 / H.264 · CRF {new X264EncodingSettings(QualityPreset, SpeedPreset, SuppressCrf).Crf} · {new X264EncodingSettings(QualityPreset, SpeedPreset).FfmpegPreset}\n音轨：兼容时保留，否则转为 AAC；开始时自动检测。";
    public void StopAnalysis() => _analysisVersion++;

    private async Task AnalyzeAsync(string path, int version)
    {
        IsAnalyzing = true;
        AnalysisError = "";
        MediaDescription = "正在分析视频信息…";
        UpdateConfigStatus();
        try
        {
            var info = await Task.Run(() =>
            {
                using var capture = new VideoCapture(path);
                if (!capture.IsOpened) throw new InvalidDataException("无法读取此视频，请检查文件是否完整或选择其他视频");
                return (Frames: (int)capture.Get(CapProp.FrameCount), Width: (int)capture.Get(CapProp.FrameWidth),
                    Height: (int)capture.Get(CapProp.FrameHeight), Fps: capture.Get(CapProp.Fps));
            });
            if (version != _analysisVersion) return;
            if (info.Frames <= 0 || info.Height <= 0) throw new InvalidDataException("未读取到有效的视频信息");
            SourceFrameCount = info.Frames;
            SourceHeight = info.Height;
            var duration = info.Fps > 0 ? TimeSpan.FromSeconds(info.Frames / info.Fps).ToString(@"hh\:mm\:ss") : "时长未知";
            MediaDescription = $"{info.Width} × {info.Height} · {info.Fps:0.##} FPS · {duration}";
        }
        catch (Exception ex)
        {
            if (version != _analysisVersion) return;
            AnalysisError = ex.Message;
            MediaDescription = "视频分析失败";
        }
        finally
        {
            if (version == _analysisVersion) { IsAnalyzing = false; UpdateConfigStatus(); }
        }
    }

    public string SourceVideo
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SourceFrameCount = 0;
            SourceHeight = 0;
            SourceSubtitle = "";
            SubtitleNotice = "";
            var guess = Path.ChangeExtension(value, ".ass");
            if (File.Exists(guess)) { SourceSubtitle = guess; SubtitleNotice = "已自动匹配同名 ASS 字幕，请确认是否使用"; }
            var version = ++_analysisVersion;
            _ = AnalyzeAsync(value, version);

            OutputPath = Path.Join(Path.GetDirectoryName(value),
                "[STVS]" + Path.GetFileNameWithoutExtension(value) + ".mp4");
            UpdateConfigStatus();
        }
    }

    public int SourceFrameCount
    {
        get => GetProperty(0);
        private set { SetProperty(value); }
    }

    public int SourceHeight
    {
        get => GetProperty(0);
        private set { SetProperty(value); }
    }

    public int EncodingThreads
    {
        get => GetProperty(0);
        set { SetProperty(value); OnPropertyChanged(nameof(TaskSummary)); }
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
        set { SetProperty(value); OnPropertyChanged(nameof(TaskSummary)); }
    }

    public VideoQualityPreset QualityPreset
    {
        get => GetProperty(VideoQualityPreset.Balanced);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(UseCustomCrf));
            OnPropertyChanged(nameof(TaskSummary));
        }
    }

    public VideoEncodingSpeedPreset SpeedPreset
    {
        get => GetProperty(VideoEncodingSpeedPreset.Balanced);
        set { SetProperty(value); OnPropertyChanged(nameof(TaskSummary)); }
    }

    public bool UseCustomCrf => QualityPreset == VideoQualityPreset.Custom;

    public bool OutputExists => File.Exists(OutputPath);
    public string SubmissionError
    {
        get => GetProperty("");
        set { SetProperty(value); }
    }

    public bool CanSubmit => !IsAnalyzing && AnalysisError.Length == 0 && File.Exists(SourceVideo)
                             && (string.IsNullOrWhiteSpace(SourceSubtitle) || File.Exists(SourceSubtitle))
                             && !string.IsNullOrWhiteSpace(OutputPath)
                             && Directory.Exists(Path.GetDirectoryName(OutputPath))
                             && !PathsEqual(SourceVideo, OutputPath);

    public string ConfigError
    {
        get
        {
            if (IsAnalyzing) return "正在分析视频，请稍候…";
            if (AnalysisError.Length > 0) return AnalysisError;
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
        new X264EncodingSettings(QualityPreset, SpeedPreset, SuppressCrf, EncodingThreads),
        SourceFrameCount, overwriteExisting, SourceHeight);

    private void UpdateConfigStatus()
    {
        OnPropertyChanged(nameof(TaskSummary));
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

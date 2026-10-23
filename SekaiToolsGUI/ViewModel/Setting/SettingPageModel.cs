using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsCore.Process.Config;
using SekaiToolsInfrastructure.Video;
using Wpf.Ui.Appearance;

namespace SekaiToolsGUI.ViewModel.Setting;

public partial class SettingPageModel : ViewModelBase
{
    public readonly List<string> CustomSpecialCharacters = [];
    private bool _suspendSaving;
    private readonly string _settingsPath;

    private SettingPageModel() : this(Path.Combine(ResourceManager.DataBaseDir, "Data", "setting.json"))
    {
    }

    internal SettingPageModel(string settingsPath)
    {
        _settingsPath = Path.GetFullPath(settingsPath);
        var availability = PeriodicColorAvailability.Value;
        IsPeriodicColorAvailable = availability.Supported;
        PeriodicColorAvailabilityReason = availability.Reason;
        LoadSetting();
    }

    private static readonly Lazy<(bool Supported, string Reason)> PeriodicColorAvailability = new(() =>
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var supported = PeriodicColorCaptureFactory.IsCurrentRuntimeSupported(out var reason);
        Logger.Log($"启动周期彩色资格检查: {timer.Elapsed.TotalMilliseconds:F0} ms; {reason}");
        return (supported, reason);
    });

    public static void InitializePeriodicColorAvailability() => _ = PeriodicColorAvailability.Value;

    private static readonly Lazy<SettingPageModel> SharedInstance = new(() => new SettingPageModel());
    public static SettingPageModel Instance => SharedInstance.Value;
    public bool IsPeriodicColorAvailable { get; }
    public string PeriodicColorAvailabilityReason { get; }
    public int PreviewPlaybackBackend
    {
        get => GetProperty(0);
        set => SetProperty(Math.Clamp(value, 0, 2));
    }

    public int RecognitionPerformanceMode
    {
        get => GetProperty(1);
        set => SetProperty(Math.Clamp(value, 0, 2));
    }

    public int RecognitionMemoryBudgetMiB
    {
        get => GetProperty(2048);
        set => SetProperty(Math.Clamp(value, 128, 4096));
    }

    public RecognitionPerformanceOptions GetRecognitionPerformanceOptions() => new()
    {
        Mode = (RecognitionPerformanceMode)RecognitionPerformanceMode,
        MemoryBudgetMiB = RecognitionMemoryBudgetMiB,
        PrepareGrayFrames = RecognitionPrepareGray,
        FrameBufferCapacity = RecognitionFrameBufferChoice switch { 1 => 4, 2 => 8, 3 => 16, _ => null }
    };
    public int RecognitionThreadChoice
    {
        get => GetProperty(0);
        set => SetProperty(value is >= 0 and <= 3 ? value : 0);
    }
    public int RecognitionFrameBufferChoice
    {
        get => GetProperty(0);
        set => SetProperty(value is >= 0 and <= 3 ? value : 0);
    }
    public bool RecognitionPrepareGray
    {
        get => GetProperty(false);
        set
        {
            if (value) RecognitionPeriodicColor = false;
            SetProperty(value);
        }
    }

    public bool RecognitionPeriodicColor
    {
        get => GetProperty(false);
        set
        {
            if (value && !IsPeriodicColorAvailable)
            {
                SetProperty(false);
                OnPropertyChanged();
                return;
            }
            if (value) RecognitionPrepareGray = false;
            SetProperty(value);
        }
    }

    public int CurrentApplicationTheme
    {
        get => GetProperty(0);
        set
        {
            if (value == 3)
            {
                // ApplicationThemeManager.Apply(ApplicationTheme.Unknown);
                // SystemThemeWatcher.Watch(Application.Current.MainWindow);
            }
            else
            {
                // SystemThemeWatcher.UnWatch(Application.Current.MainWindow);
                switch (value)
                {
                    case 0:
                        ApplicationThemeManager.Apply(ApplicationTheme.Light);
                        break;
                    case 1:
                        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                        break;
                    case 2:
                        ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                        break;
                }
            }

            SetProperty(value);
        }
    }

    public int ProxyType
    {
        get => GetProperty(0);
        set
        {
            ProxyChangeable = value == 0 ? Visibility.Collapsed : Visibility.Visible;
            SetProperty(value);
        }
    }

    public Visibility ProxyChangeable
    {
        get => GetProperty(Visibility.Collapsed);
        set => SetProperty(value);
    }

    public string ProxyHost
    {
        get => GetProperty("127.0.0.1");
        set => SetProperty(value);
    }

    public int ProxyPort
    {
        get => GetProperty(1080);
        set => SetProperty(value);
    }

    public string DownloadDirectory
    {
        get => GetProperty(Model.Setting.Default.DownloadDirectory);
        set => SetProperty(value ?? "");
    }

    public string GetDownloadDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(DownloadDirectory)
            ? Model.Setting.Default.DownloadDirectory : DownloadDirectory.Trim();
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("下载文件存储目录必须是绝对路径，请在设置中重新选择。");
        return Path.GetFullPath(directory);
    }

    public int TypewriterFadeTime
    {
        get => GetProperty(50);
        set => SetProperty(value);
    }

    public int TypewriterCharTime
    {
        get => GetProperty(80);
        set => SetProperty(value);
    }


    public string DialogFontFamily
    {
        get => GetProperty("思源黑体 CN Bold");
        set => SetProperty(value);
    }

    public string BannerFontFamily
    {
        get => GetProperty("思源黑体 Medium");
        set => SetProperty(value);
    }

    public string MarkerFontFamily
    {
        get => GetProperty("思源黑体 Medium");
        set => SetProperty(value);
    }

    public bool ExportLine1
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine2
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine3
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportCharacter
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportScreenComment
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public static string AppVersion
        => (Application.ResourceAssembly.GetName().Version ??
            new Version(0, 0, 0, 0)).ToString();

    public ObservableCollection<LogEntry> LogEntries => InMemoryLogSink.Entries;

    private new void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        base.SetProperty(value, propertyName);
        if (!_suspendSaving)
            SaveSetting();
    }

    public Proxy GetProxy()
    {
        return new Proxy(ProxyHost, ProxyPort, ProxyType switch
        {
            0 => Proxy.Type.None,
            1 => Proxy.Type.Http,
            2 => Proxy.Type.Socks5,
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    public ExportStyleConfig GetExportStyleConfig()
    {
        return new ExportStyleConfig
        {
            ExportLine1 = ExportLine1,
            ExportLine2 = ExportLine2,
            ExportLine3 = ExportLine3,
            ExportCharacter = ExportCharacter,
            ExportBannerMask = ExportBannerMask,
            ExportBannerText = ExportBannerText,
            ExportMarkerMask = ExportMarkerMask,
            ExportMarkerText = ExportMarkerText,
            ExportScreenComment = ExportScreenComment
        };
    }

    public StyleFontConfig GetStyleFontConfig()
    {
        return new StyleFontConfig
        {
            DialogFontFamily = DialogFontFamily,
            BannerFontFamily = BannerFontFamily,
            MarkerFontFamily = MarkerFontFamily
        };
    }

    public TypewriterSetting GetTypewriterSetting()
    {
        return new TypewriterSetting
        {
            FadeTime = TypewriterFadeTime,
            CharTime = TypewriterCharTime
        };
    }
}

partial class SettingPageModel
{
    private string GetSettingPath()
    {
        return _settingsPath;
    }

    public void SaveSetting()
    {
        try
        {
            var setting = Model.Setting.FromModel(this);
            var settingPath = GetSettingPath();
            var directory = Path.GetDirectoryName(settingPath)!;
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory, $".setting.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, setting.Dump(), Encoding.UTF8);
                File.Move(tempPath, settingPath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception exception)
        {
            Logger.Log($"设置保存失败: {exception.Message}", LogLevel.Error);
        }
    }

    public void ResetSetting()
    {
        _suspendSaving = true;
        try
        {
            CurrentApplicationTheme = Model.Setting.Default.CurrentApplicationTheme;
            ProxyType = Model.Setting.Default.ProxyType;
            ProxyHost = Model.Setting.Default.ProxyHost;
            ProxyPort = Model.Setting.Default.ProxyPort;
            DownloadDirectory = Model.Setting.Default.DownloadDirectory;
            PreviewPlaybackBackend = Model.Setting.Default.PreviewPlaybackBackend;
            RecognitionPerformanceMode = Model.Setting.Default.RecognitionPerformanceMode;
            RecognitionMemoryBudgetMiB = Model.Setting.Default.RecognitionMemoryBudgetMiB;
            RecognitionPrepareGray = Model.Setting.Default.RecognitionPrepareGray;
            RecognitionPeriodicColor = Model.Setting.Default.RecognitionPeriodicColor;
            RecognitionThreadChoice = Model.Setting.Default.RecognitionThreadChoice;
            RecognitionFrameBufferChoice = Model.Setting.Default.RecognitionFrameBufferChoice;

            TypewriterFadeTime = Model.Setting.Default.TypewriterFadeTime;
            TypewriterCharTime = Model.Setting.Default.TypewriterCharTime;

            DialogFontFamily = Model.Setting.Default.DialogFontFamily;
            BannerFontFamily = Model.Setting.Default.BannerFontFamily;
            MarkerFontFamily = Model.Setting.Default.MarkerFontFamily;

            ExportLine1 = Model.Setting.Default.ExportLine1;
            ExportLine2 = Model.Setting.Default.ExportLine2;
            ExportLine3 = Model.Setting.Default.ExportLine3;
            ExportCharacter = Model.Setting.Default.ExportCharacter;
            ExportBannerMask = Model.Setting.Default.ExportBannerMask;
            ExportBannerText = Model.Setting.Default.ExportBannerText;
            ExportMarkerMask = Model.Setting.Default.ExportMarkerMask;
            ExportMarkerText = Model.Setting.Default.ExportMarkerText;
            ExportScreenComment = Model.Setting.Default.ExportScreenComment;
        }
        finally
        {
            _suspendSaving = false;
        }

        SaveSetting();
    }

    public void LoadSetting()
    {
        Model.Setting setting;
        try
        {
            setting = Model.Setting.Load(GetSettingPath());
        }
        catch (Exception)
        {
            setting = new Model.Setting();
        }

        _suspendSaving = true;
        try
        {
            CurrentApplicationTheme = setting.CurrentApplicationTheme;
            CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
            ProxyType = setting.ProxyType;
            ProxyHost = setting.ProxyHost;
            ProxyPort = setting.ProxyPort;
            DownloadDirectory = string.IsNullOrWhiteSpace(setting.DownloadDirectory)
                ? Model.Setting.Default.DownloadDirectory : setting.DownloadDirectory;
            PreviewPlaybackBackend = setting.PreviewPlaybackBackend;
            RecognitionPerformanceMode = setting.RecognitionPerformanceMode;
            RecognitionMemoryBudgetMiB = setting.RecognitionMemoryBudgetMiB;
            RecognitionPrepareGray = setting.RecognitionPrepareGray;
            RecognitionPeriodicColor = setting.RecognitionPeriodicColor;
            RecognitionThreadChoice = setting.RecognitionThreadChoice;
            RecognitionFrameBufferChoice = setting.RecognitionFrameBufferChoice;

            TypewriterFadeTime = setting.TypewriterFadeTime;
            TypewriterCharTime = setting.TypewriterCharTime;

            DialogFontFamily = setting.DialogFontFamily == ""
                ? Model.Setting.Default.DialogFontFamily
                : setting.DialogFontFamily;
            BannerFontFamily = setting.BannerFontFamily == ""
                ? Model.Setting.Default.BannerFontFamily
                : setting.BannerFontFamily;
            MarkerFontFamily = setting.MarkerFontFamily == ""
                ? Model.Setting.Default.MarkerFontFamily
                : setting.MarkerFontFamily;

            ExportLine1 = setting.ExportLine1;
            ExportLine2 = setting.ExportLine2;
            ExportLine3 = setting.ExportLine3;
            ExportCharacter = setting.ExportCharacter;
            ExportBannerMask = setting.ExportBannerMask;
            ExportBannerText = setting.ExportBannerText;
            ExportMarkerMask = setting.ExportMarkerMask;
            ExportMarkerText = setting.ExportMarkerText;
            ExportScreenComment = setting.ExportScreenComment;
        }
        finally
        {
            _suspendSaving = false;
        }
        if (setting.RecognitionPeriodicColor && !IsPeriodicColorAvailable)
            SaveSetting();
    }
}

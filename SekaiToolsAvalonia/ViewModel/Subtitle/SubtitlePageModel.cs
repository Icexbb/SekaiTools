using Avalonia.Media.Imaging;

namespace SekaiToolsAvalonia.ViewModel.Subtitle;

public class SubtitlePageModel : ViewModelBase
{
    public string VideoFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
        }
    }

    public string ScriptFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
        }
    }

    public string TranslateFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
        }
    }

    public Bitmap? FramePreviewImage
    {
        get => GetProperty<Bitmap?>();
        set => SetProperty(value);
    }

    public bool IsRunning
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
            UpdateRunningStatus();
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public bool IsFinished
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
            UpdateRunningStatus();
            OnPropertyChanged(nameof(CanOutput));
            OnPropertyChanged(nameof(CanReset));
        }
    }

    public bool IsCanceled
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
            UpdateRunningStatus();
            OnPropertyChanged(nameof(CanOutput));
            OnPropertyChanged(nameof(CanReset));
        }
    }

    public bool IsFailed
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
            UpdateRunningStatus();
            OnPropertyChanged(nameof(CanReset));
        }
    }

    public bool IsCanceling
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            NotifyResetEnabled();
            UpdateRunningStatus();
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public bool CanOutput => IsFinished || IsCanceled;
    public bool CanReset => IsFinished || IsCanceled || IsFailed;
    public bool CanStop => IsRunning && !IsCanceling;

    public string RunningStatus
    {
        get => GetProperty("未开始");
        private set => SetProperty(value);
    }

    public bool ResetEnabled
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool HasNotStarted
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowPreview
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowTooLongOnly
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool ShowDialog
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowBanner
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowMarker
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public int DialogTotal
    {
        get => GetProperty(100);
        set => SetProperty(value);
    }

    public int DialogCurrent
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public int BannerTotal
    {
        get => GetProperty(100);
        set => SetProperty(value);
    }

    public int BannerCurrent
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public int MarkerTotal
    {
        get => GetProperty(100);
        set => SetProperty(value);
    }

    public int MarkerCurrent
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    private void UpdateRunningStatus()
    {
        if (IsCanceled)
            RunningStatus = "已取消";
        else if (IsFinished)
            RunningStatus = "已完成";
        else if (IsFailed)
            RunningStatus = "处理失败";
        else if (IsCanceling)
            RunningStatus = "正在取消";
        else if (IsRunning)
            RunningStatus = "处理中";
        else
            RunningStatus = "未开始";
    }

    public void Reset()
    {
        VideoFilePath = "";
        ScriptFilePath = "";
        TranslateFilePath = "";
        IsRunning = false;
        IsFinished = false;
        IsCanceled = false;
        IsFailed = false;
        IsCanceling = false;
        HasNotStarted = true;
        FramePreviewImage = null;
        DialogTotal = 100;
        DialogCurrent = 0;
        BannerTotal = 100;
        BannerCurrent = 0;
        MarkerTotal = 100;
        MarkerCurrent = 0;
    }

    private void NotifyResetEnabled()
    {
        ResetEnabled = VideoFilePath != "" || ScriptFilePath != "" || TranslateFilePath != "" ||
                       IsRunning || IsFinished || IsCanceled || IsFailed || IsCanceling;
    }
}
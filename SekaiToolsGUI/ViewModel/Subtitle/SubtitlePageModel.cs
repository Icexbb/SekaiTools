using System.Windows;
using System.Windows.Media;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class SubtitlePageModel : ViewModelBase
{
    public string VideoFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SetResetEnabled();
            VideoFileName = System.IO.Path.GetFileName(value);
            OnPropertyChanged(nameof(CanStart));
        }
    }

    public string VideoFileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }


    public string ScriptFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SetResetEnabled();
            ScriptFileName = System.IO.Path.GetFileName(value);
            OnPropertyChanged(nameof(CanStart));
        }
    }

    public string ScriptFileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string TranslateFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SetResetEnabled();
            TranslateFileName = System.IO.Path.GetFileName(value);
            OnPropertyChanged(nameof(CanStart));
        }
    }

    public string TranslateFileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }


    public ImageSource FramePreviewImage
    {
        get => GetProperty<ImageSource>(Mat.Zeros(100, 100, DepthType.Cv8U, 4).ToBitmapSource());
        set => SetProperty(value);
    }

    public bool IsRunning
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            SetResetEnabled();
            SetRunningStatus();
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public bool IsFinished
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            SetResetEnabled();
            SetRunningStatus();
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
            SetResetEnabled();
            SetRunningStatus();
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
            SetResetEnabled();
            SetRunningStatus();
            OnPropertyChanged(nameof(CanReset));
        }
    }

    public bool IsPartial
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            SetResetEnabled();
            SetRunningStatus();
            OnPropertyChanged(nameof(CanOutput));
            OnPropertyChanged(nameof(CanReset));
        }
    }

    public bool IsCanceling
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            SetResetEnabled();
            SetRunningStatus();
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public bool CanOutput => IsFinished || IsCanceled || IsPartial;
    public bool CanReset => IsFinished || IsCanceled || IsPartial || IsFailed;
    public bool CanStop => IsRunning && !IsCanceling;
    public bool CanStart => !string.IsNullOrWhiteSpace(VideoFilePath) &&
                            !string.IsNullOrWhiteSpace(ScriptFilePath) &&
                            !string.IsNullOrWhiteSpace(TranslateFilePath);

    public string RunningStatus
    {
        get => GetProperty("未开始");
        private set => SetProperty(value);
    }

    public Visibility ResetEnabled
    {
        get => GetProperty(Visibility.Collapsed);
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
        set
        {
            SetProperty(value);
            ShowDialogLine1 = value;
            ShowDialogLine2 = value;
            ShowDialogLine3 = value;
        }
    }

    public bool ShowDialogLine1
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowDialogLine2
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowDialogLine3
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


    private void SetRunningStatus()
    {
        if (IsCanceled)
            RunningStatus = "已取消";
        else if (IsPartial)
            RunningStatus = "部分完成";
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
        IsPartial = false;
        IsCanceling = false;
        HasNotStarted = true;
        FramePreviewImage = Mat.Zeros(100, 100, DepthType.Cv8U, 4).ToBitmapSource();

        DialogTotal = 100;
        DialogCurrent = 0;
        BannerTotal = 100;
        BannerCurrent = 0;
        MarkerTotal = 100;
        MarkerCurrent = 0;
    }

    private void SetResetEnabled()
    {
        if (VideoFilePath != "" || ScriptFilePath != "" || TranslateFilePath != "" ||
            IsRunning || IsFinished || IsCanceled || IsPartial || IsFailed || IsCanceling)
            ResetEnabled = Visibility.Visible;
        else
            ResetEnabled = Visibility.Collapsed;
    }
}

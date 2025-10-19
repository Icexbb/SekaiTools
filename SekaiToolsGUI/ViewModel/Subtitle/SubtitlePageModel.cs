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
        }
    }

    public string ScriptFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SetResetEnabled();
        }
    }

    public string TranslateFilePath
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            SetResetEnabled();
        }
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
        }
    }

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
        if (IsFinished)
            RunningStatus = "已完成";
        else if (IsRunning)
            RunningStatus = "运行中";
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
            IsRunning || IsFinished)
            ResetEnabled = Visibility.Visible;
        else
            ResetEnabled = Visibility.Collapsed;
    }
}
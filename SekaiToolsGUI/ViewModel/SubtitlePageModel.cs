using System.Windows;
using System.Windows.Media;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsGUI.ViewModel;

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
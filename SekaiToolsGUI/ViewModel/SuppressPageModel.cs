using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsGUI.View.Suppress;

namespace SekaiToolsGUI.ViewModel;

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
            if (File.Exists(value))
            {
                using var capture = new VideoCapture(value);
                SourceFrameCount = (int)capture.Get(CapProp.FrameCount);
            }

            var guess = Path.ChangeExtension(value, ".ass");
            if (File.Exists(guess)) SourceSubtitle = guess;

            OutputPath = Path.Join(Path.GetDirectoryName(value), Path.GetFileNameWithoutExtension(value) + "_h264.mp4");
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

    private bool GetCanStartSuppress => !string.IsNullOrWhiteSpace(SourceVideo) &&
                                        // !string.IsNullOrWhiteSpace(SourceSubtitle) &&
                                        !string.IsNullOrWhiteSpace(OutputPath);

    public bool CanStartSuppress
    {
        get => GetProperty(GetCanStartSuppress);
        set => SetProperty(value);
    }


    public bool HasNotStarted
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool Running
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

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

    private void UpdateConfigStatus()
    {
        CanStartSuppress = GetCanStartSuppress;
    }

    public void ReloadStatus()
    {
        Status = "";
        Progression = 0;
        HasNotStarted = true;
        Running = false;
        Fps = 0;
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
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace SekaiToolsGUI.View.Suppress;

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
            if (File.Exists(guess))
            {
                SourceSubtitle = guess;
            }

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
                                        !string.IsNullOrWhiteSpace(SourceSubtitle) &&
                                        !string.IsNullOrWhiteSpace(OutputPath);

    private void UpdateConfigStatus()
    {
        CanStartSuppress = GetCanStartSuppress;
    }

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

public partial class SuppressPage : UserControl, INavigableView<SuppressPageModel>
{
    public SuppressPageModel ViewModel => (SuppressPageModel)DataContext;

    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();
    }

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        ViewModel.SourceVideo = result;
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "字幕文件|*.ass");
        if (result == null) return;

        ViewModel.SourceSubtitle = result;
    }

    private void SaveFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectSavePath();
        if (result == null) return;

        ViewModel.OutputPath = result;
    }

    private string? SelectSavePath()
    {
        var openFileDialog = new SaveFileDialog
        {
            Filter = "Mp4 文件|*.mp4;",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.SourceVideo),
            DefaultExt = ".mp4",
            FileName = Path.ChangeExtension(Path.GetFileName(ViewModel.SourceVideo), ".txt")
        };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private async void StartSuppress_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Suppressor.Instance.Suppress();
        }
        catch (Exception exc)
        {
            await Dispatcher.Invoke<Task>(async () =>
            {
                var uiMessageBox = new MessageBox
                {
                    Title = "视频处理出错",
                    Content = exc.Message + "\n" + exc.StackTrace
                };

                await uiMessageBox.ShowDialogAsync();
                ViewModel.Running = false;
            });
            if (Debugger.IsAttached) throw;
        }
    }


    private void DisposeButton_OnClick(object sender, RoutedEventArgs e)
    {
        Suppressor.Instance.Clean();
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        Suppressor.Instance.Clean();
        ViewModel.Reset();
    }

    private void ShowFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowFile(ViewModel.OutputPath);
        return;

        void ShowFile(string path)
        {
            var psi = new ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    private void StatusTextChange_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (Wpf.Ui.Controls.TextBox)sender!;
        box.ScrollToEnd();
    }
}
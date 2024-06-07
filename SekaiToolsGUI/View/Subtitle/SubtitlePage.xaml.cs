using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SekaiToolsGUI.View.Subtitle;

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

    public Visibility ResetEnabled
    {
        get => GetProperty(Visibility.Collapsed);
        set => SetProperty(value);
    }

    private void SetResetEnabled()
    {
        if (VideoFilePath != "" || ScriptFilePath != "" || TranslateFilePath != "" || IsRunning || IsFinished)
            ResetEnabled = Visibility.Visible;
        else
            ResetEnabled = Visibility.Collapsed;
    }

    public bool HasNotStarted
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }
}

public partial class SubtitlePage : UserControl, INavigableView<SubtitlePageModel>
{
    public SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;

    public SubtitlePage()
    {
        DataContext = new SubtitlePageModel();
        InitializeComponent();
    }

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private async void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        ViewModel.VideoFilePath = result;

        var scriptPath1 = Path.ChangeExtension(result, ".json");
        var scriptPath2 = Path.ChangeExtension(result, ".asset");
        var translatePath = Path.ChangeExtension(result, ".txt");
        if (File.Exists(scriptPath1) || File.Exists(scriptPath2) || File.Exists(translatePath))
        {
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
            var token = new CancellationToken();
            var dialogResult = await dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "在该文件处发现了同名的文件，是否自动引入作为剧本/翻译文件？",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否",
                }, token);
            if (dialogResult != ContentDialogResult.Primary) return;

            if (File.Exists(scriptPath1)) ViewModel.ScriptFilePath = scriptPath1;
            else if (File.Exists(scriptPath2)) ViewModel.ScriptFilePath = scriptPath2;
            if (File.Exists(translatePath)) ViewModel.TranslateFilePath = translatePath;
        }
    }


    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result != null) ViewModel.ScriptFilePath = result;
    }

    private void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result != null) ViewModel.TranslateFilePath = result;
    }

    private VideoCapture? _videoCapture;
    private MatcherCreator? _matcherCreator;
    private DialogMatcher? _dialogMatcher;
    private ContentMatcher? _contentMatcher;
    private BannerMatcher? _bannerMatcher;

    private Task? _task;
    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken CancellationToken => _cancellationTokenSource!.Token;


    private void ControlButton_OnClick(object sender, EventArgs arg)
    {
        if (CheckConfig()) StartProcess();
        return;

        bool CheckConfig()
        {
            var vfp = ViewModel.VideoFilePath;
            var sfp = ViewModel.ScriptFilePath;
            var tfp = ViewModel.TranslateFilePath;
            var service = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;
            if (string.IsNullOrEmpty(vfp) || string.IsNullOrEmpty(sfp) || string.IsNullOrEmpty(tfp))
            {
                service.Show("错误", "请填写完整的文件路径", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.TextGrammarDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(vfp))
            {
                service.Show("错误", "视频文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(sfp))
            {
                service.Show("错误", "剧情脚本文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(tfp))
            {
                service.Show("错误", "剧情翻译文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            return true;
        }
    }


    private void StartProcess()
    {
        _matcherCreator =
            new MatcherCreator(ViewModel.VideoFilePath, ViewModel.ScriptFilePath, ViewModel.TranslateFilePath);
        _videoCapture = new VideoCapture(ViewModel.VideoFilePath);
        _dialogMatcher = _matcherCreator.DialogMatcher();
        _contentMatcher = _matcherCreator.ContentMatcher();
        _bannerMatcher = _matcherCreator.BannerMatcher();

        ViewModel.IsRunning = true;
        ViewModel.HasNotStarted = false;

        _task = new Task(Process, CancellationToken);
        _task.Start();
        _task.ContinueWith(task =>
        {
            Dispatcher.Invoke(() =>
            {
                var snackService = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

                ViewModel.IsRunning = false;
                if (_contentMatcher == null || _dialogMatcher == null || _bannerMatcher == null) return;
                if (!_contentMatcher.Finished || !_dialogMatcher.Finished || !_bannerMatcher.Finished)
                {
                    snackService.Show("错误", "运行结束", ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                }
                else
                {
                    ViewModel.IsFinished = true;
                    snackService.Show("成功", "运行结束", ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
                }
            });
        }, CancellationToken);
    }

    private void Process()
    {
        if (_videoCapture == null || _videoCapture.Ptr == IntPtr.Zero) return;
        if (_dialogMatcher == null || _contentMatcher == null || _bannerMatcher == null) return;
        var frameRate = _videoCapture.Get(CapProp.Fps);
        var frame = new Mat();
        while (true)
        {
            if (CancellationToken.IsCancellationRequested) break;
            if (!_videoCapture.Read(frame)) break;

            CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);
            var frameIndex = (int)_videoCapture.Get(CapProp.PosFrames);
            if (frameIndex % ((int)frameRate / 5) == 0)
                Dispatcher.Invoke(() => { ViewModel.FramePreviewImage = frame.Clone().ToBitmapSource(); },
                    DispatcherPriority.Normal, CancellationToken);
            if (!_contentMatcher.Finished)
            {
                _contentMatcher.Process(frame);
                continue;
            }

            var matchBanner = false;
            if (!_dialogMatcher.Finished)
            {
                var dialogIndex = _dialogMatcher.LastNotProcessedIndex();
                var r = _dialogMatcher.Process(frame, frameIndex);
                matchBanner = !r;
                if (_dialogMatcher.Set[dialogIndex].Finished)
                    LinePanel_AddDialogLine(_dialogMatcher.Set[dialogIndex]);
            }

            if (matchBanner)
            {
                if (!_bannerMatcher.Finished)
                {
                    var bannerIndex = _bannerMatcher.LastNotProcessedIndex();
                    _bannerMatcher.Process(frame, frameIndex);
                    if (_bannerMatcher.Set[bannerIndex].Finished)
                        LinePanel_AddBannerLine(_bannerMatcher.Set[bannerIndex]);
                }
            }
        }
    }

    private void LinePanel_AddDialogLine(DialogFrameSet set)
    {
        Console.WriteLine($"{set.Start().Index}->{set.End().Index}");
        Dispatcher.Invoke(() =>
        {
            var binding = new Binding("IsChecked")
            {
                Source = OnlyTooLongSwitch,
                Converter = new InverseBooleanToVisibilityConverter(),
                Mode = BindingMode.OneWay,
            };
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            if (!line.ViewModel.NeedSetSeparator)
                line.SetBinding(VisibilityProperty, binding);
            LinePanel.Children.Add(line);
            LineViewer.ScrollToEnd();
        });
    }

    private void LinePanel_AddBannerLine(BannerFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var binding = new Binding("IsChecked")
            {
                Source = OnlyTooLongSwitch,
                Converter = new InverseBooleanToVisibilityConverter(),
                Mode = BindingMode.OneWay,
            };
            var line = new BannerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            line.SetBinding(VisibilityProperty, binding);
            LinePanel.Children.Add(line);
            LineViewer.ScrollToEnd();
        });
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.Reset();
        LinePanel.Children.Clear();
    }

    private void StopProcess()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        _task = null;

        _videoCapture?.Dispose();
        _videoCapture = null;
        _matcherCreator = null;
        _dialogMatcher = null;
        _contentMatcher = null;
        _bannerMatcher = null;
    }

    private async void OutputButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new SaveFileDialog(dialogService.GetContentPresenter(), ViewModel.VideoFilePath);
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;
        var fileName = dialog.ViewModel.FileName;

        var subtitle = GenerateSubtitle();
        await File.WriteAllTextAsync(fileName, subtitle.ToString(), token);

        var snackService = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;
        snackService.Show("成功", "字幕文件已保存", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
        ShowFile(fileName);

        return;

        void ShowFile(string path)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            System.Diagnostics.Process.Start(psi);
        }

        SekaiToolsCore.SubStationAlpha.Subtitle GenerateSubtitle()
        {
            List<BannerFrameSet> bannerFrameSets = [];
            List<DialogFrameSet> dialogFrameSets = [];
            foreach (var child in LinePanel.Children)
            {
                switch (child)
                {
                    case DialogLine dialogLine:
                        dialogFrameSets.Add(dialogLine.ViewModel.Set);
                        break;
                    case BannerLine bannerLine:
                        bannerFrameSets.Add(bannerLine.ViewModel.Set);
                        break;
                }
            }

            var maker = _matcherCreator!.SubtitleMaker();
            return maker.Make(dialogFrameSets, bannerFrameSets);
        }
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Collapsed;
    }
}
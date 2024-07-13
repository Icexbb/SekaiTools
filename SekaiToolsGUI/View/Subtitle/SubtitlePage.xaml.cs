using System.Diagnostics;
using System.IO;
using System.Text;
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
using SekaiToolsCore.Story;
using SekaiToolsCore.Story.Event;
using SekaiToolsGUI.View.Setting;
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
        if (VideoFilePath != "" || ScriptFilePath != "" || TranslateFilePath != "" ||
            IsRunning || IsFinished)
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

    private async Task SelectSameNameFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();

        string[] videoExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        string[] jsonExt = [".json", ".asset"];
        string[] txtExt = [".txt"];

        if (videoExt.Contains(fileExt))
        {
            ViewModel.VideoFilePath = filename;

            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (scriptPath == null && translatePath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (jsonExt.Contains(fileExt))
        {
            ViewModel.ScriptFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);

            if (videoPath == null && translatePath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (txtExt.Contains(fileExt))
        {
            ViewModel.TranslateFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (videoPath == null && scriptPath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
        }

        return;

        async Task<bool> ShowDialog()
        {
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
            var token = new CancellationToken();
            var dialogResult = await dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "在该文件处发现了同名的文件，是否自动引入作为处理文件？",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否",
                }, token);
            return dialogResult == ContentDialogResult.Primary;
        }
    }


    private async void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        await SelectSameNameFile(result);
    }

    private async void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result == null) return;

        await SelectSameNameFile(result);
    }

    private async void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result == null) return;

        await SelectSameNameFile(result);
    }

    private VideoCapture? _videoCapture;
    private MatcherCreator? _matcherCreator;
    private DialogMatcher? _dialogMatcher;
    private ContentMatcher? _contentMatcher;
    private BannerMatcher? _bannerMatcher;
    private MarkerMatcher? _markerMatcher;

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
        var settings = new SettingPageModel();
        _matcherCreator =
            new MatcherCreator(new Config(
                ViewModel.VideoFilePath,
                ViewModel.ScriptFilePath,
                ViewModel.TranslateFilePath,
                settings.FontFamily,
                settings.ExportComment,
                new TypewriterSetting(
                    int.Min(settings.TypewriterFadeTime, settings.TypewriterCharTime),
                    int.Max(settings.TypewriterFadeTime, settings.TypewriterCharTime)),
                new MatchingThreshold(settings.ThresholdNormal, settings.ThresholdSpecial)
            ));
        _videoCapture = new VideoCapture(ViewModel.VideoFilePath);
        _dialogMatcher = _matcherCreator.DialogMatcher();
        _contentMatcher = _matcherCreator.ContentMatcher();
        _bannerMatcher = _matcherCreator.BannerMatcher();
        _markerMatcher = _matcherCreator.MarkerMatcher();

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
                if (_contentMatcher == null || _dialogMatcher == null ||
                    _bannerMatcher == null || _markerMatcher == null) return;
                if (!_contentMatcher.Finished || !_dialogMatcher.Finished ||
                    !_bannerMatcher.Finished || !_markerMatcher.Finished)
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

                TextBlockETA.Text = "";
            });
        }, CancellationToken);
    }

    private void Process()
    {
        if (_videoCapture == null || _videoCapture.Ptr == IntPtr.Zero) return;
        if (_dialogMatcher == null || _contentMatcher == null ||
            _bannerMatcher == null || _markerMatcher == null) return;
        var frameRate = _videoCapture.Get(CapProp.Fps);
        var frame = new Mat();
        if (_matcherCreator == null) throw new NullReferenceException();
        var frameCount = _videoCapture.Get(CapProp.FrameCount);
        var markerIndexInDialog = MarkerIndexOfDialog();

        var avgDuration = 0d;
        var frameIndex = 0;
        var updateTime = 0;
        while (true)
        {
            var tic = Environment.TickCount;
            try
            {
                if (CancellationToken.IsCancellationRequested) break;
                if (_videoCapture is not { IsOpened: true }) break;
                if (!_videoCapture.Read(frame)) break;

                frameIndex = (int)_videoCapture.Get(CapProp.PosFrames);
                if (frameIndex % ((int)frameRate / 10) == 0)
                {
                    UpdateProgress(frameIndex / frameCount);
                    Dispatcher.Invoke(() => { ViewModel.FramePreviewImage = frame.Clone().ToBitmapSource(); },
                        DispatcherPriority.Normal, CancellationToken);
                }

                CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);

                if (!_contentMatcher.Finished)
                {
                    _contentMatcher.Process(frame);
                    continue;
                }

                var matchBannerNow = true;
                if (!_dialogMatcher.Finished)
                {
                    var dialogIndex = _dialogMatcher.LastNotProcessedIndex();
                    var r = _dialogMatcher.Process(frame, frameIndex);
                    matchBannerNow = !r;
                    if (_dialogMatcher.Set[dialogIndex].Finished)
                        LinePanel_AddDialogLine(_dialogMatcher.Set[dialogIndex]);
                }

                if (!_bannerMatcher.Finished && matchBannerNow)
                {
                    var bannerIndex = _bannerMatcher.LastNotProcessedIndex();
                    _bannerMatcher.Process(frame, frameIndex);
                    if (_bannerMatcher.Set[bannerIndex].Finished)
                        LinePanel_AddBannerLine(_bannerMatcher.Set[bannerIndex]);
                }

                if (!_markerMatcher.Finished && MatchMarkerNow())
                {
                    var markerIndex = _markerMatcher.LastNotProcessedIndex();
                    _markerMatcher.Process(frame, frameIndex);
                    if (_markerMatcher.Set[markerIndex].Finished)
                        LinePanel_AddMarkerLine(_markerMatcher.Set[markerIndex]);
                }
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(async () =>
                {
                    var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "视频处理出错",

                        Content = e.Message + "\n" + e.StackTrace,
                    };

                    await uiMessageBox.ShowDialogAsync(cancellationToken: CancellationToken);
                    ViewModel.IsRunning = false;
                });
                if (Debugger.IsAttached) throw;
            }
            finally
            {
                var toc = Environment.TickCount;
                Fps(toc - tic);
            }
        }

        UpdateProgress(1);

        return;

        bool MatchMarkerNow()
        {
            if (_markerMatcher!.Set.Count == 0) return false;
            var markerIndex = _markerMatcher!.LastNotProcessedIndex();
            var dialogIndex = _dialogMatcher!.LastNotProcessedIndex();
            if (dialogIndex < 0) return true;
            return dialogIndex >= markerIndexInDialog[markerIndex];
        }


        List<int> MarkerIndexOfDialog()
        {
            var dialogCount = -1;
            var markerIndex = new List<int>();
            var events = new Queue<Event>(
                _matcherCreator!.Story.GetTypes(Story.StoryEventType.Dialog | Story.StoryEventType.Marker)
            );
            while (events.TryDequeue(out var ev))
            {
                switch (ev)
                {
                    case Dialog:
                        dialogCount += 1;
                        break;
                    case Marker:
                        markerIndex.Add(dialogCount);
                        break;
                }
            }

            return markerIndex.Select(x => x < 0 ? 0 : x).ToList();
        }

        void UpdateProgress(double progression)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBarProgression.Value = progression;
                ProgressBarProgression.Maximum = 1;
                TextBlockProgression.Text = $"{progression:P}";
            });
        }

        void Fps(int deltaTime)
        {
            const double alpha = 1d / 100d; // 采样数设置为100

            if (Math.Abs(frameCount - 1) < double.MinValue)
                avgDuration = deltaTime;
            else
                avgDuration = avgDuration * (1 - alpha) + deltaTime * alpha;

            updateTime += deltaTime;

            Dispatcher.Invoke(() =>
            {
                if (TextBlockFps.Text != "" && TextBlockETA.Text != "")
                    if (updateTime < 1000)
                    {
                        return;
                    }
                    else updateTime = 0;

                var etaMs = (frameCount - frameIndex) * avgDuration;
                var eta = new TimeSpan(0, 0, 0, 0, (int)etaMs);
                TextBlockFps.Text = $"FPS: {(int)(1d / avgDuration * 1000)}";
                TextBlockETA.Text = etaMs >= 1000 ? $"ETA: {eta.Remains()}" : "";
            });
        }
    }

    private void LinePanel_AddDialogLine(DialogFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private Binding GetOnlyTooLongBinding()
    {
        var binding = new Binding("IsChecked")
        {
            Source = OnlyTooLongSwitch,
            Converter = new InverseBooleanToVisibilityConverter(),
            Mode = BindingMode.OneWay,
        };
        return binding;
    }

    private void LinePanel_AddBannerLine(BannerFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new BannerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private void LinePanel_AddMarkerLine(MarkerFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new MarkerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.Reset();
        LinePanel.Children.Clear();
        TextBlockProgression.Text = "";
        TextBlockFps.Text = "";
        ProgressBarProgression.Value = 0;
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
        await File.WriteAllTextAsync(fileName, subtitle.ToString(),
            Encoding.UTF8, token);

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
            List<MarkerFrameSet> markerFrameSets = [];
            foreach (var child in LinePanel.Children)
            {
                switch (child)
                {
                    case DialogLine dialogLine:
                        var set = dialogLine.ViewModel.Set;
                        set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("…", "..."); // 修正省略号
                        dialogFrameSets.Add(dialogLine.ViewModel.Set);
                        break;
                    case BannerLine bannerLine:
                        bannerFrameSets.Add(bannerLine.ViewModel.Set);
                        break;
                    case MarkerLine markerLine:
                        markerFrameSets.Add(markerLine.ViewModel.Set);
                        break;
                }
            }

            if (_matcherCreator == null) throw new NullReferenceException();
            var maker = _matcherCreator.SubtitleMaker();
            return maker.Make(dialogFrameSets, bannerFrameSets, markerFrameSets);
        }
    }

    private async void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        var data = e.Data.GetData(DataFormats.FileDrop)!;
        var fileName = ((Array)data).GetValue(0)!.ToString();
        if (!File.Exists(fileName)) return;

        await GetSameBaseFile(fileName);
    }

    private async Task GetSameBaseFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();
        List<string> vExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        List<string> sExt = [".json", ".asset"];
        List<string> tExt = [".txt"];
        if (vExt.Contains(fileExt) || sExt.Contains(fileExt) || tExt.Contains(fileExt))
            await SelectSameNameFile(filename);
        else
            ShowSnackError();

        return;

        void ShowSnackError()
        {
            var snackService = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;
            snackService.Show("错误", "文件格式不支持", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentError24),
                new TimeSpan(0, 0, 3));
        }
    }

    private void UIElement_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Link
            : DragDropEffects.None;
    }

    private void OnlyTooLongSwitch_OnClick(object sender, RoutedEventArgs e)
    {
        var targetVisiblity = (OnlyTooLongSwitch.IsChecked ?? false) ? Visibility.Collapsed : Visibility.Visible;
        foreach (var child in LinePanel.Children)
        {
            switch (child)
            {
                case DialogLine dialogLine:
                    if (dialogLine.ViewModel.Set.NeedSetSeparator) continue;
                    dialogLine.Visibility = targetVisiblity;
                    break;
                case BannerLine bannerLine:
                    bannerLine.Visibility = targetVisiblity;
                    break;
                case MarkerLine markerLine:
                    markerLine.Visibility = targetVisiblity;
                    break;
            }
        }
    }
}
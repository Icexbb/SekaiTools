using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Emgu.CV;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Utils;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.View.Subtitle.Components;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using SaveFileDialog = SekaiToolsGUI.View.Subtitle.Components.SaveFileDialog;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace SekaiToolsGUI.View.Subtitle;

public partial class SubtitlePage : UserControl, IAppPage<SubtitlePageModel>
{
    public SubtitlePage()
    {
        DataContext = new SubtitlePageModel();
        InitializeComponent();
        SubscribeFpsChange();
        SubscribeProgressChange();
    }


    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;


    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
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
                    CloseButtonText = "否"
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

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.Reset();
        LinePanel.Children.Clear();
        TextBlockProgression.Text = "";
        TextBlockFps.Text = "";
        ProgressBarProgression.Value = 0;
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.IsRunning = false;
        ViewModel.IsFinished = true;
    }

    private void StartButton_OnClick(object sender, EventArgs arg)
    {
        try
        {
            if (CheckConfig()) StartProcess();
        }
        catch (Exception ex)
        {
            SnackService.Show("错误", $"启动处理失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
        }

        return;

        bool CheckConfig()
        {
            var vfp = ViewModel.VideoFilePath;
            var sfp = ViewModel.ScriptFilePath;
            var tfp = ViewModel.TranslateFilePath;
            if (string.IsNullOrEmpty(vfp) || string.IsNullOrEmpty(sfp) || string.IsNullOrEmpty(tfp))
            {
                SnackService.Show("错误", "请填写完整的文件路径", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.TextGrammarDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(vfp))
            {
                SnackService.Show("错误", "视频文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(sfp))
            {
                SnackService.Show("错误", "剧情脚本文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(tfp))
            {
                SnackService.Show("错误", "剧情翻译文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            return true;
        }
    }


    private void LinePanel_AddDialogLine(DialogBaseFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            ViewModel.DialogCurrent++;
            RefreshContentVisibility();
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }


    private void LinePanel_AddBannerLine(BannerBaseFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new BannerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            ViewModel.BannerCurrent++;
            RefreshContentVisibility();
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private void LinePanel_AddMarkerLine(MarkerBaseFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new MarkerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            ViewModel.MarkerCurrent++;
            RefreshContentVisibility();
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }


    private async void OutputButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new SaveFileDialog(dialogService.GetDialogHostEx() ?? throw new InvalidOperationException(),
            ViewModel.VideoFilePath);
        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;
        var fileName = dialog.ViewModel.FileName;

        try
        {
            var subtitle = GenerateSubtitle();

            var staffText = BuildStaffLineText(dialog.ViewModel);
            if (!string.IsNullOrWhiteSpace(staffText))
            {
                var startTime = "0:00:00.00";
                var totalSec = dialog.ViewModel.StaffLineTime;
                var h = (int)(totalSec / 3600);
                var m = (int)(totalSec / 60) % 60;
                var s = (int)totalSec % 60;
                var cs = (int)((totalSec - (int)totalSec) * 100);
                var endTime = $"{h}:{m:00}:{s:00}.{cs:00}";
                var staffEvent = SekaiToolsBase.SubStationAlpha.Event.Dialog(
                    $"{{\\an{dialog.ViewModel.StaffLinePosition}}}{staffText}",
                    startTime, endTime, "Screen");
                subtitle.Events.Insert(0, staffEvent);
            }

            await File.WriteAllTextAsync(fileName, subtitle.ToString(), Encoding.UTF8, token);

            SnackService.Show("成功", "字幕文件已保存", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
            ExplorerHelper.OpenFolderAndFocus(fileName);
        }
        catch (Exception ex)
        {
            SnackService.Show("错误", $"保存字幕文件失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
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
            SnackService.Show("错误", "文件格式不支持", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentError24),
                new TimeSpan(0, 0, 3));
    }

    private void UIElement_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Link
            : DragDropEffects.None;
    }

    private void FilterSwitch_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshContentVisibility();
    }

    private void RefreshContentVisibility()
    {
        foreach (var child in LinePanel.Children)
            switch (child)
            {
                case DialogLine dialogLine:
                    if (dialogLine.ViewModel.Set.NeedSetSeparator)
                        dialogLine.Visibility = ViewModel.ShowDialog ? Visibility.Visible : Visibility.Collapsed;
                    else
                        dialogLine.Visibility = ViewModel is { ShowDialog: true, ShowTooLongOnly: false }
                            ? Visibility.Visible
                            : Visibility.Collapsed;

                    break;
                case BannerLine bannerLine:
                    bannerLine.Visibility = ViewModel.ShowBanner ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case MarkerLine markerLine:
                    markerLine.Visibility = ViewModel.ShowMarker ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
    }
}

public partial class SubtitlePage
{
    private CancellationTokenSource? TokenSource { get; } = new();
    private CancellationToken CancellationToken => TokenSource!.Token;

    private VideoProcessor? VideoProcessor { get; set; }
    private Subject<(int Fps, TimeSpan Eta)>? _fpsChangedSubject;
    private IDisposable? _fpsChangedSubscription;
    private Subject<double>? _progressChangedSubject;
    private IDisposable? _progressChangedSubscription;

    public async void OnNavigatedTo()
    {
        try
        {
            if (await ResourceManager.Instance.CheckResource(ResourceType.VideoProcess)) return;
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
            var dialog = new RefreshWaitDialog("正在刷新下载源数据");
            var source = new CancellationTokenSource();
            _ = dialogService.ShowAsync(dialog, source.Token);
            await ResourceManager.Instance.EnsureResource(ResourceType.VideoProcess);
            await source.CancelAsync();
        }
        catch (Exception e)
        {
            (Application.Current.MainWindow as MainWindow)?.OnCheckResourceFailed(e, OnNavigatedTo);
        }
    }

    private void StopProcess()
    {
        VideoProcessor?.StopProcess();
    }

    private static string BuildStaffLineText(SaveFileDialogModel model)
    {
        if (model.StaffLineTime <= 0) return string.Empty;

        var entries = new List<(string Label, string Value)>();

        AddIfNotEmpty("录制", model.StaffLineRecord);
        AddIfNotEmpty("翻译", model.StaffLineTranslator);
        AddIfNotEmpty("校对", model.StaffLineTranslatorSenior);
        AddIfNotEmpty("时轴", model.StaffLineTimeline);
        AddIfNotEmpty("轴校", model.StaffLineTimelineSenior);
        AddIfNotEmpty("压制", model.StaffLineCompression);


        var parts = entries
            .GroupBy(e => e.Value)
            .Select(g => $"{string.Join("/", g.Select(e => e.Label))}：{g.Key}")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var prefix = model.StaffLinePrefix;
        var suffix = model.StaffLineSuffix;
        List<string> allParts = [];
        if (!string.IsNullOrWhiteSpace(prefix)) allParts.Add(prefix.Trim());
        allParts.AddRange(parts);
        if (!string.IsNullOrWhiteSpace(suffix)) allParts.Add(suffix.Trim());
        return string.Join("\\N", allParts).Trim();

        void AddIfNotEmpty(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                entries.Add((label, value.Trim()));
        }
    }

    private SekaiToolsBase.SubStationAlpha.Subtitle GenerateSubtitle()
    {
        List<BannerBaseFrameSet> bannerFrameSets = [];
        List<DialogBaseFrameSet> dialogFrameSets = [];
        List<MarkerBaseFrameSet> markerFrameSets = [];
        foreach (var child in LinePanel.Children)
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

        if (VideoProcessor == null) throw new NullReferenceException();
        return VideoProcessor.GenerateSubtitle(bannerFrameSets, dialogFrameSets, markerFrameSets);
    }

    private MatchingThreshold GetMatchingThreshold()
    {
        var thresholdData = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, "thresholds.json");
        if (!File.Exists(thresholdData)) return new MatchingThreshold();
        var json = File.ReadAllText(thresholdData);
        return JsonSerializer.Deserialize<MatchingThreshold>(json);
    }

    private void StartProcess()
    {
        var settings = SettingPageModel.Instance;

        Logger.Log(
            $"开始处理: 视频={ViewModel.VideoFilePath}, 剧本={ViewModel.ScriptFilePath}, 翻译={ViewModel.TranslateFilePath}",
            LogLevel.Information);
        try
        {
            VideoProcessor = new VideoProcessor(new Config(
                    ViewModel.VideoFilePath,
                    ViewModel.ScriptFilePath,
                    ViewModel.TranslateFilePath,
                    settings.GetStyleFontConfig(),
                    settings.GetExportStyleConfig(),
                    settings.GetTypewriterSetting(),
                    GetMatchingThreshold()
                ), new VideoProcessCallbacks
                {
                    OnTaskFinished = () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ViewModel.IsRunning = false;
                            if (!VideoProcessor?.Finished ?? false)
                            {
                                var stopReason = VideoProcessor?.StopReason;
                                var errorMsg = stopReason switch
                                {
                                    SekaiToolsCore.ProcessStopReason.Canceled => "用户中止处理",
                                    SekaiToolsCore.ProcessStopReason.ReadFailed => "视频读帧失败",
                                    SekaiToolsCore.ProcessStopReason.ExceptionThreshold => "异常过多，自动中止",
                                    SekaiToolsCore.ProcessStopReason.CaptureError => "视频捕获设备出错",
                                    _ => "未知错误"
                                };
                                Logger.Log($"处理异常结束: {stopReason}", LogLevel.Warning);
                                SnackService.Show("错误", errorMsg, ControlAppearance.Danger,
                                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                            }
                            else
                            {
                                ViewModel.IsFinished = true;
                                Logger.Log("处理成功完成", LogLevel.Information);
                                SnackService.Show("成功", "运行结束", ControlAppearance.Success,
                                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
                            }

                            TextBlockEta.Text = "";
                        });
                    },
                    OnTaskStarted = () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ViewModel.IsRunning = true;
                            ViewModel.HasNotStarted = false;
                            var contentLength = VideoProcessor?.ContentLength;
                            if (contentLength != null)
                            {
                                ViewModel.DialogTotal = contentLength.Dialog;
                                ViewModel.DialogCurrent = 0;
                                ViewModel.BannerTotal = contentLength.Banner;
                                ViewModel.BannerCurrent = 0;
                                ViewModel.MarkerTotal = contentLength.Marker;
                                ViewModel.MarkerCurrent = 0;
                            }
                            else
                            {
                                ViewModel.DialogTotal = 0;
                                ViewModel.BannerTotal = 0;
                                ViewModel.MarkerTotal = 0;
                            }
                        });
                    },
                    OnProgress = progression => { OnProgressChanged(progression); },
                    OnFramePreviewImage = frame =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (ViewModel.ShowPreview)
                                ViewModel.FramePreviewImage = frame.ToBitmapSource();
                        });
                    },
                    OnNewDialog = LinePanel_AddDialogLine,
                    OnNewBanner = LinePanel_AddBannerLine,
                    OnNewMarker = LinePanel_AddMarkerLine,
                    OnException = e =>
                    {
                        Logger.Log($"视频处理异常: {e.Message}\n{e.StackTrace}", LogLevel.Error);
                        Dispatcher.Invoke(async () =>
                        {
                            var uiMessageBox = new MessageBox
                            {
                                Title = "视频处理出错",
                                Content = e.Message + "\n" + e.StackTrace
                            };

                            await uiMessageBox.ShowDialogAsync(cancellationToken: CancellationToken);
                            ViewModel.IsRunning = false;
                        });
                    },
                    OnFps = OnFpsChanged
                }
            );
            VideoProcessor.StartProcess();
        }
        catch (Exception ex)
        {
            Logger.Log($"初始化视频处理器失败: {ex.Message}", LogLevel.Error);
            SnackService.Show("错误", $"初始化视频处理器失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
        }
    }

    private void OnFpsChanged(int fps, TimeSpan eta)
    {
        _fpsChangedSubject?.OnNext((fps, eta));
    }

    private void OnProgressChanged(double progression)
    {
        _progressChangedSubject?.OnNext(progression);
    }

    private void SubscribeFpsChange()
    {
        _fpsChangedSubscription?.Dispose();
        _fpsChangedSubject?.OnCompleted();
        _fpsChangedSubject?.Dispose();
        _fpsChangedSubject = new Subject<(int Fps, TimeSpan Eta)>();
        _fpsChangedSubscription = _fpsChangedSubject
            ?.Sample(TimeSpan.FromMilliseconds(200))
            .Subscribe(x =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TextBlockFps.Text = $"FPS: {x.Fps}";
                    TextBlockEta.Text = x.Eta.TotalMilliseconds > 1000 ? $"ETA: {x.Eta.Remains()}" : "";
                });
            });
    }

    private void SubscribeProgressChange()
    {
        _progressChangedSubscription?.Dispose();
        _progressChangedSubject?.OnCompleted();
        _progressChangedSubject?.Dispose();
        _progressChangedSubject = new Subject<double>();
        _progressChangedSubscription = _progressChangedSubject
            .Sample(TimeSpan.FromMilliseconds(200))
            .Subscribe(value =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ProgressBarProgression.Value = value;
                    ProgressBarProgression.Maximum = 1;
                    TextBlockProgression.Text = $"{value:P}";
                });
            });
    }


    private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.ShowPreview = false;
    }

    private void ShowPreviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowPreview = true;
    }
}
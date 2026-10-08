using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emgu.CV;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsBase.SubStationAlpha;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using SaveFileDialog = SekaiToolsGUI.View.Subtitle.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTask : UserControl
{
    private bool _isResetting;
    private readonly DispatcherBatchQueue _resultQueue;
    private bool _previewEnabled;
    private bool _disposed;
    internal Func<bool>? CanDeleteSavedProgress { get; set; }
    public event EventHandler? RemoveRequested;

    internal async Task RunAsync(ProcessingState? savedState)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ViewModel.CanStart || !File.Exists(ViewModel.VideoFilePath) ||
            !File.Exists(ViewModel.ScriptFilePath) || !File.Exists(ViewModel.TranslateFilePath))
            throw new FileNotFoundException("字幕任务的视频、剧本或翻译文件不存在");

        ViewModel.HasNotStarted = false;
        ViewModel.IsRunning = true;
        try
        {
            StartProcess(ProgressStore.GetSaveKey(ViewModel.VideoFilePath, ViewModel.ScriptFilePath,
                ViewModel.TranslateFilePath), savedState);
            if (VideoProcessor != null) await VideoProcessor.WaitForCompletionAsync();
            await _resultQueue.FlushAsync();
        }
        finally { ReleaseSubtitlePowerRequest(); }
    }

    internal void RequestStop() => VideoProcessor?.StopProcess();
    internal async Task DisposeAsync()
    {
        if (_disposed) return;
        await ReleaseProcessorAsync();
        _disposed = true;
        _fpsChangedSubscription?.Dispose();
        _progressChangedSubscription?.Dispose();
        _fpsChangedSubject?.Dispose();
        _progressChangedSubject?.Dispose();
        ReleaseSubtitlePowerRequest();
        TokenSource?.Cancel();
        TokenSource?.Dispose();
        ProcessView.EventTimelineEditor.ClearSelection();
        ProcessView.LinePanel.Children.Clear();
    }

    private void SubtitleTask_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ProcessView?.UpdateViewport(e.NewSize.Height);
    }

    public SubtitleTask() : this(new SubtitlePageModel()) { }

    public SubtitleTask(SubtitlePageModel state)
    {
        DataContext = state;
        InitializeComponent();
        _previewEnabled = ViewModel.ShowPreview;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SubtitlePageModel.ShowPreview))
                Volatile.Write(ref _previewEnabled, ViewModel.ShowPreview);
        };
        _resultQueue = new DispatcherBatchQueue(Dispatcher, () => ProcessView.EventTimelineEditor.DeferRendering());
        SubscribeFpsChange();
        SubscribeProgressChange();
    }


    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;

    internal void LoadHistoryState(ProcessingState state)
    {
        var settings = SettingPageModel.Instance;
        try
        {
            VideoProcessor?.Dispose();
            VideoProcessor = new VideoProcessor(new Config(
                state.VideoFilePath,
                state.ScriptFilePath,
                state.TranslateFilePath,
                settings.GetStyleFontConfig(),
                settings.GetExportStyleConfig(),
                settings.GetTypewriterSetting(),
                GetMatchingThreshold(),
                settings.GetRecognitionPerformanceOptions()
            ), new VideoProcessCallbacks
            {
                OnNewDialog = LinePanel_AddDialogLine,
                OnNewBanner = LinePanel_AddBannerLine,
                OnNewMarker = LinePanel_AddMarkerLine
            }, ResourceManager.Instance, ProcessingStatePersistence.Instance);

            SetTimelineVideoDuration();
            VideoProcessor.ApplyState(state);
            VideoProcessor.ReplayExportableCallbacks(
                LinePanel_AddDialogLine,
                LinePanel_AddBannerLine,
                LinePanel_AddMarkerLine);

            ViewModel.DialogTotal = VideoProcessor.ContentLength.Dialog;
            ViewModel.BannerTotal = VideoProcessor.ContentLength.Banner;
            ViewModel.MarkerTotal = VideoProcessor.ContentLength.Marker;
            ViewModel.HasNotStarted = false;
            var resultReport = VideoProcessor.ResultReport;
            var isPartial = resultReport.Outcome != ProcessingOutcome.Complete || !resultReport.CanExport;
            ViewModel.IsFinished = !isPartial;
            ViewModel.IsCanceled = state.StopReason == ProcessStopReason.Canceled;
            ViewModel.IsFailed = isPartial && !ViewModel.IsCanceled && !resultReport.CanExport;
            ViewModel.IsPartial = isPartial && !ViewModel.IsCanceled && !ViewModel.IsFailed;
            var frameCount = state.Metadata?.VideoInfo.FrameCount ?? 0;
            ProcessView.ProgressBarProgression.Value = isPartial && frameCount > 0
                ? Math.Clamp((double)state.FrameIndex / frameCount, 0, 1)
                : 1;
            ProcessView.ProgressBarProgression.Maximum = 1;
            ViewModel.Progress = ProcessView.ProgressBarProgression.Value;
            ProcessView.TextBlockProgression.Text = $"{ProcessView.ProgressBarProgression.Value:P}";
        }
        catch (Exception ex)
        {
            ViewModel.IsRunning = false;
            ViewModel.IsCanceling = false;
            ViewModel.IsFinished = false;
            ViewModel.IsCanceled = false;
            ViewModel.IsPartial = false;
            ViewModel.IsFailed = true;
            ViewModel.HasNotStarted = false;
            SnackService.Show("错误", $"加载历史记录失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
        }
    }

    private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isResetting) return;
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "移除当前任务？",
            Content = "当前处理结果和手动调整将从界面清除。",
            PrimaryButtonText = "移除",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    internal async Task ResetCurrentTaskAsync()
    {
        if (_isResetting) return;
        _isResetting = true;
        try
        {
            await ReleaseProcessorAsync();
            ViewModel.Reset();
            ProcessView.LinePanel.Children.Clear();
            ProcessView.EventTimelineEditor.ClearSelection();
            ProcessView.TextBlockProgression.Text = "";
            ProcessView.TextBlockFps.Text = "";
            ProcessView.TextBlockEta.Text = "";
            ProcessView.ProgressBarProgression.Value = 0;
            await TaskMemoryCleanup.AfterResetAsync(Dispatcher);
        }
        finally
        {
            _isResetting = false;
        }
    }

    private async Task ReleaseProcessorAsync()
    {
        var processor = VideoProcessor;
        if (processor == null) return;
        try
        {
            await processor.StopProcessAsync();
            await _resultQueue.FlushAsync();
        }
        catch (Exception exception)
        {
            Logger.Log($"重置时处理任务异常结束: {exception}", LogLevel.Error);
        }
        finally
        {
            processor.Dispose();
            VideoProcessor = null;
            // Do not retain the disposed task graph across the reset's GC await.
            processor = null;
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.IsCanceling = true;
    }

    private void LinePanel_InsertInOriginalOrder(UIElement line, int eventIndex)
    {
        var insertionIndex = 0;
        while (insertionIndex < ProcessView.LinePanel.Children.Count &&
               GetLineEventIndex(ProcessView.LinePanel.Children[insertionIndex]) <= eventIndex)
            insertionIndex++;

        ProcessView.LinePanel.Children.Insert(insertionIndex, line);
    }

    private static int GetLineEventIndex(UIElement line)
    {
        return line switch
        {
            DialogLine dialogLine => dialogLine.ViewModel.EventIndex,
            BannerLine bannerLine => bannerLine.ViewModel.EventIndex,
            MarkerLine markerLine => markerLine.ViewModel.EventIndex,
            _ => int.MaxValue
        };
    }


    private void LinePanel_AddDialogLine(DialogBaseFrameSet set)
    {
        _resultQueue.Enqueue(() =>
        {
            var needScroll = Math.Abs(ProcessView.LineViewer.ScrollableHeight - ProcessView.LineViewer.VerticalOffset) < 1;
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                ProcessView.EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => ProcessView.EventTimelineEditor.SelectEvent(timelineEvent);
            }

            line.PreviewRequested += async (_, _) =>
            {
                if (_disposed || !ViewModel.CanPreviewLine) return;
                await ProcessView.EventTimelineEditor.PreviewEventVideoAsync(
                    ViewModel.VideoFilePath, CreateTimelineEvent(line));
            };
            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
            ViewModel.DialogCurrent++;
            ProcessView.RefreshContentVisibility();
            if (needScroll) ProcessView.LineViewer.ScrollToEnd();
        });
    }


    private void LinePanel_AddBannerLine(BannerBaseFrameSet set)
    {
        _resultQueue.Enqueue(() =>
        {
            var needScroll = Math.Abs(ProcessView.LineViewer.ScrollableHeight - ProcessView.LineViewer.VerticalOffset) < 1;

            var line = new BannerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                ProcessView.EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => ProcessView.EventTimelineEditor.SelectEvent(timelineEvent);
            }

            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
            ViewModel.BannerCurrent++;
            ProcessView.RefreshContentVisibility();
            if (needScroll) ProcessView.LineViewer.ScrollToEnd();
        });
    }

    private void LinePanel_AddMarkerLine(MarkerBaseFrameSet set)
    {
        _resultQueue.Enqueue(() =>
        {
            var needScroll = Math.Abs(ProcessView.LineViewer.ScrollableHeight - ProcessView.LineViewer.VerticalOffset) < 1;

            var line = new MarkerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                ProcessView.EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => ProcessView.EventTimelineEditor.SelectEvent(timelineEvent);
            }

            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
            ViewModel.MarkerCurrent++;
            ProcessView.RefreshContentVisibility();
            if (needScroll) ProcessView.LineViewer.ScrollToEnd();
        });
    }

    private TimelineEventSelection CreateTimelineEvent(DialogLine line)
    {
        var accent = line.ViewModel.SpeakerBrush
                     ?? TryFindResource("AccentFillColorDefaultBrush") as Brush
                     ?? Brushes.DodgerBlue;
        return new TimelineEventSelection(
            line.ViewModel.Set,
            line.ViewModel.EventNumber,
            "对话",
            TimelineEventTrack.Dialog,
            GetTimelineContent(line.ViewModel.RawContent, line.ViewModel.TranslatedContent),
            accent,
            line.RefreshTiming,
            line.BringIntoView);
    }

    private TimelineEventSelection CreateTimelineEvent(BannerLine line)
    {
        return new TimelineEventSelection(
            line.ViewModel.Set,
            line.ViewModel.EventNumber,
            "横幅",
            TimelineEventTrack.Banner,
            GetTimelineContent(line.ViewModel.RawContent, line.ViewModel.TranslatedContent),
            Brushes.Gray,
            line.ViewModel.RefreshTiming,
            line.BringIntoView);
    }

    private TimelineEventSelection CreateTimelineEvent(MarkerLine line)
    {
        return new TimelineEventSelection(
            line.ViewModel.Set,
            line.ViewModel.EventNumber,
            "标记",
            TimelineEventTrack.Marker,
            GetTimelineContent(line.ViewModel.RawContent, line.ViewModel.TranslatedContent),
            Brushes.Gray,
            line.ViewModel.RefreshTiming,
            line.BringIntoView);
    }

    private static string GetTimelineContent(string original, string translated)
    {
        return string.IsNullOrWhiteSpace(translated) ? original : translated;
    }

    private void SetTimelineVideoDuration()
    {
        if (!GeneralFunctionSwitch.EventTimeline || VideoProcessor == null)
            return;

        var videoInfo = VideoProcessor.VideoInfo;
        var fps = videoInfo.Fps.Fps();
        var durationMilliseconds = fps > 0
            ? (int)Math.Ceiling(videoInfo.FrameCount * 1000d / fps)
            : 0;
        ProcessView.EventTimelineEditor.SetVideoDuration(durationMilliseconds);
        _ = ProcessView.EventTimelineEditor.LoadAudioWaveformAsync(videoInfo.Path);
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
                var staffEvent = Event.Dialog(
                    $"{{\\an{dialog.ViewModel.StaffLinePosition}}}{staffText}",
                    startTime, endTime, "staff");
                subtitle.Events.Insert(0, staffEvent);
            }

            await File.WriteAllTextAsync(fileName, subtitle.ToString(), Encoding.UTF8, token);

            if (CanDeleteSavedProgress?.Invoke() == true)
            {
                var saveKey = ProgressStore.GetSaveKey(ViewModel.VideoFilePath, ViewModel.ScriptFilePath,
                    ViewModel.TranslateFilePath);
                SubtitleQueueProgressStore.Delete(saveKey);
                ProgressStore.Delete(saveKey);
            }

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

}

public partial class SubtitleTask
{
    private Subject<(int Fps, TimeSpan Eta)>? _fpsChangedSubject;
    private IDisposable? _fpsChangedSubscription;
    private Subject<double>? _progressChangedSubject;
    private IDisposable? _progressChangedSubscription;
    private IDisposable? _subtitlePowerRequest;
    private CancellationTokenSource? TokenSource { get; } = new();
    private CancellationToken CancellationToken => TokenSource!.Token;

    private VideoProcessor? VideoProcessor { get; set; }

    internal static async Task EnsureResourcesAsync()
    {
        if (await ResourceManager.Instance.CheckResource(ResourceType.VideoProcess)) return;

        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在准备字幕识别资源");
        using var source = new CancellationTokenSource();
        var dialogTask = dialogService.ShowAsync(dialog, source.Token);
        try
        {
            await ResourceManager.Instance.EnsureResource(ResourceType.VideoProcess, source.Token);
        }
        finally
        {
            await source.CancelAsync();
            try
            {
                await dialogTask;
            }
            catch (OperationCanceledException)
            {
                // 对话框由取消令牌关闭。
            }
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
        foreach (var child in ProcessView.LinePanel.Children)
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

    private void StartProcess(string saveKey, ProcessingState? resumeState)
    {
        var settings = SettingPageModel.Instance;

        Logger.Log(
            $"开始处理: 视频={ViewModel.VideoFilePath}, 剧本={ViewModel.ScriptFilePath}, 翻译={ViewModel.TranslateFilePath}");
        try
        {
            VideoProcessor?.Dispose();
            ViewModel.IsRunning = true;
            VideoProcessor = new VideoProcessor(new Config(
                    ViewModel.VideoFilePath,
                    ViewModel.ScriptFilePath,
                    ViewModel.TranslateFilePath,
                    settings.GetStyleFontConfig(),
                    settings.GetExportStyleConfig(),
                    settings.GetTypewriterSetting(),
                    GetMatchingThreshold(),
                    settings.GetRecognitionPerformanceOptions()
                ), new VideoProcessCallbacks
                {
                    OnTaskFinished = () =>
                    {
                        _resultQueue.FlushAsync().GetAwaiter().GetResult();
                        ReleaseSubtitlePowerRequest();
                        Dispatcher.Invoke(() =>
                        {
                            ViewModel.IsRunning = false;
                            ViewModel.IsCanceling = false;
                            var stopReason = VideoProcessor?.StopReason;
                            var resultReport = VideoProcessor?.ResultReport;
                            if (stopReason == ProcessStopReason.Canceled)
                            {
                                ViewModel.IsCanceled = true;
                                Logger.Log("处理已由用户取消，可输出当前结果");
                                SnackService.Show("提示", "处理已取消，可以输出当前结果进行人工复核",
                                    ControlAppearance.Info,
                                    new SymbolIcon(SymbolRegular.Info24), new TimeSpan(0, 0, 4));
                            }
                            else if (stopReason == ProcessStopReason.Completed &&
                                     resultReport is { Outcome: ProcessingOutcome.Complete, CanExport: true })
                            {
                                ViewModel.IsFinished = true;
                                ProcessView.ProgressBarProgression.Value = 1;
                                ViewModel.Progress = 1;
                                ProcessView.ProgressBarProgression.Maximum = 1;
                                ProcessView.TextBlockProgression.Text = $"{1:P}";
                                Logger.Log("处理成功完成");
                                SnackService.Show("成功", "运行结束", ControlAppearance.Success,
                                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
                            }
                            else if (resultReport is { CanExport: true })
                            {
                                ViewModel.IsPartial = true;
                                Logger.Log($"处理部分完成: {resultReport.Summary}", LogLevel.Warning);
                                SnackService.Show("警告",
                                    $"处理未完整结束，已识别 {resultReport.RecognizedTotal}/{resultReport.Total} 项，可输出当前结果进行人工复核",
                                    ControlAppearance.Caution,
                                    new SymbolIcon(SymbolRegular.Warning24), new TimeSpan(0, 0, 5));
                            }
                            else
                            {
                                ViewModel.IsFailed = true;
                                var errorMsg = stopReason switch
                                {
                                    ProcessStopReason.Completed => "未识别到可导出的字幕事件",
                                    ProcessStopReason.ReadFailed => "视频读帧失败",
                                    ProcessStopReason.ExceptionThreshold => "异常过多，自动中止",
                                    ProcessStopReason.CaptureError => "视频捕获设备出错",
                                    ProcessStopReason.UnexpectedError => "视频处理发生未预期错误",
                                    _ => "未知错误"
                                };
                                Logger.Log($"处理异常结束: {stopReason}", LogLevel.Warning);
                                SnackService.Show("错误", errorMsg, ControlAppearance.Danger,
                                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                            }

                            ProcessView.TextBlockEta.Text = "";
                        });
                    },
                    OnTaskStarted = () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ViewModel.IsFinished = false;
                            ViewModel.IsCanceled = false;
                            ViewModel.IsFailed = false;
                            ViewModel.IsPartial = false;
                            ViewModel.IsCanceling = false;
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
                    IsPreviewEnabled = () => Volatile.Read(ref _previewEnabled),
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
                        });
                    },
                    OnFps = OnFpsChanged
                },
                ResourceManager.Instance,
                SubtitleQueueProcessingStatePersistence.Instance
            );

            SetTimelineVideoDuration();
            if (resumeState != null)
            {
                VideoProcessor.ApplyState(resumeState);
                VideoProcessor.ReplayFinishedCallbacks(
                    LinePanel_AddDialogLine,
                    LinePanel_AddBannerLine,
                    LinePanel_AddMarkerLine);
            }

            VideoProcessor.EnableProgressSaving(saveKey);
            ReleaseSubtitlePowerRequest();
            _subtitlePowerRequest = SystemPowerRequest.Acquire("SekaiTools 正在识别字幕");
            VideoProcessor.StartProcess();
        }
        catch (Exception ex)
        {
            ViewModel.IsRunning = false;
            ReleaseSubtitlePowerRequest();
            ViewModel.IsCanceling = false;
            ViewModel.IsFinished = false;
            ViewModel.IsCanceled = false;
            ViewModel.IsPartial = false;
            ViewModel.IsFailed = true;
            ViewModel.HasNotStarted = false;
            Logger.Log($"初始化视频处理器失败: {ex.Message}", LogLevel.Error);
            SnackService.Show("错误", $"初始化视频处理器失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
        }
    }

    private void ReleaseSubtitlePowerRequest()
    {
        Interlocked.Exchange(ref _subtitlePowerRequest, null)?.Dispose();
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
                    if (_disposed) return;
                    ProcessView.TextBlockFps.Text = $"FPS: {x.Fps}";
                    ProcessView.TextBlockEta.Text = x.Eta.TotalMilliseconds > 1000 ? $"ETA: {x.Eta.Remains()}" : "";
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
                    if (_disposed || !ViewModel.IsRunning) return;

                    ProcessView.ProgressBarProgression.Value = value;
                    ViewModel.Progress = value;
                    ProcessView.ProgressBarProgression.Maximum = 1;
                    ProcessView.TextBlockProgression.Text = $"{value:P}";
                });
            });
    }

}

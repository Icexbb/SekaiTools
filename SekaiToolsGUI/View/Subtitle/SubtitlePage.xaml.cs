using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Emgu.CV;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsBase.SubStationAlpha;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.General;
using SekaiToolsGUI.View.Subtitle.Components;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using SaveFileDialog = SekaiToolsGUI.View.Subtitle.Components.SaveFileDialog;

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

    private async Task CheckSavedProgressOnStartup()
    {
        var progressFiles = ProgressStore.EnumerateProgressFiles();
        foreach (var (saveKey, state) in progressFiles)
        {
            if (string.IsNullOrEmpty(state.VideoFilePath) ||
                string.IsNullOrEmpty(state.ScriptFilePath) ||
                string.IsNullOrEmpty(state.TranslateFilePath))
                continue;
            if (!File.Exists(state.VideoFilePath) ||
                !File.Exists(state.ScriptFilePath) ||
                !File.Exists(state.TranslateFilePath))
                continue;

            var result = await ShowResumeDialogAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.VideoFilePath = state.VideoFilePath;
                ViewModel.ScriptFilePath = state.ScriptFilePath;
                ViewModel.TranslateFilePath = state.TranslateFilePath;
                StartProcess(saveKey, state);
            }
            else
            {
                ProgressStore.Delete(saveKey);
            }

            return;
        }
    }

    private async Task ShowHistoryDialogAsync()
    {
        var entries = HistoryStore.LoadAll();
        if (entries.Count == 0)
        {
            SnackService.Show("提示", "暂无历史记录", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24), new TimeSpan(0, 0, 3));
            return;
        }

        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new HistoryDialog(dialogService.GetDialogHostEx() ?? throw new InvalidOperationException(),
            entries);
        var result = await dialogService.ShowAsync(dialog, CancellationToken);

        if (result == ContentDialogResult.Primary && dialog.SelectedEntry != null)
        {
            LinePanel.Children.Clear();
            EventTimelineEditor.ClearSelection();
            ViewModel.DialogCurrent = 0;
            ViewModel.BannerCurrent = 0;
            ViewModel.MarkerCurrent = 0;

            var state = dialog.SelectedEntry.State;
            ViewModel.VideoFilePath = state.VideoFilePath;
            ViewModel.ScriptFilePath = state.ScriptFilePath;
            ViewModel.TranslateFilePath = state.TranslateFilePath;
            LoadHistoryState(state);
        }
    }

    private void LoadHistoryState(ProcessingState state)
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
                GetMatchingThreshold()
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
            var isPartial = resultReport.Outcome != ProcessingOutcome.Complete;
            ViewModel.IsFinished = !isPartial;
            ViewModel.IsPartial = isPartial;
            var frameCount = state.Metadata?.VideoInfo.FrameCount ?? 0;
            ProgressBarProgression.Value = isPartial && frameCount > 0
                ? Math.Clamp((double)state.FrameIndex / frameCount, 0, 1)
                : 1;
            ProgressBarProgression.Maximum = 1;
            TextBlockProgression.Text = $"{ProgressBarProgression.Value:P}";
            SetVideoProcessWindowTitle(isPartial ? "部分完成" : "已完成");
            SetTaskbarProgressState(isPartial ? TaskbarItemProgressState.Paused : TaskbarItemProgressState.Normal,
                ProgressBarProgression.Value);
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
            SetVideoProcessWindowTitle("处理失败");
            SetTaskbarProgressState(TaskbarItemProgressState.Error, ProgressBarProgression.Value);
            SnackService.Show("错误", $"加载历史记录失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 5));
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

    private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "重置当前任务？",
            Content = "当前处理结果和手动调整将从界面清除。",
            PrimaryButtonText = "重置",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        StopProcess();
        (Application.Current.MainWindow as MainWindow)?.SetWindowTitle("");
        SetTaskbarProgressState(TaskbarItemProgressState.None, 0);
        VideoProcessor?.Dispose();
        VideoProcessor = null;
        ViewModel.Reset();
        LinePanel.Children.Clear();
        EventTimelineEditor.ClearSelection();
        TextBlockProgression.Text = "";
        TextBlockFps.Text = "";
        ProgressBarProgression.Value = 0;
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        SetVideoProcessWindowTitle("正在取消");
        SetTaskbarProgressState(TaskbarItemProgressState.Paused, ProgressBarProgression.Value);
        ViewModel.IsCanceling = true;
    }

    private async void HistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ShowHistoryDialogAsync();
    }

    private void StartButton_OnClick(object sender, EventArgs arg)
    {
        try
        {
            if (!CheckConfig()) return;

            var saveKey = ProgressStore.GetSaveKey(
                ViewModel.VideoFilePath,
                ViewModel.ScriptFilePath,
                ViewModel.TranslateFilePath);

            StartProcess(saveKey, null);
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


    private void LinePanel_InsertInOriginalOrder(UIElement line, int eventIndex)
    {
        var insertionIndex = 0;
        while (insertionIndex < LinePanel.Children.Count &&
               GetLineEventIndex(LinePanel.Children[insertionIndex]) <= eventIndex)
            insertionIndex++;

        LinePanel.Children.Insert(insertionIndex, line);
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
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => EventTimelineEditor.SelectEvent(timelineEvent);
            }

            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
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
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => EventTimelineEditor.SelectEvent(timelineEvent);
            }

            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
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
            if (GeneralFunctionSwitch.EventTimeline)
            {
                var timelineEvent = CreateTimelineEvent(line);
                EventTimelineEditor.RegisterEvent(timelineEvent);
                line.TimelineRequested += (_, _) => EventTimelineEditor.SelectEvent(timelineEvent);
            }

            LinePanel_InsertInOriginalOrder(line, line.ViewModel.EventIndex);
            ViewModel.MarkerCurrent++;
            RefreshContentVisibility();
            if (needScroll) LineViewer.ScrollToEnd();
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
        EventTimelineEditor.SetVideoDuration(durationMilliseconds);
        _ = EventTimelineEditor.LoadAudioWaveformAsync(videoInfo.Path);
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
                    startTime, endTime, "Staff");
                subtitle.Events.Insert(0, staffEvent);
            }

            await File.WriteAllTextAsync(fileName, subtitle.ToString(), Encoding.UTF8, token);

            ProgressStore.Delete(ProgressStore.GetSaveKey(
                ViewModel.VideoFilePath, ViewModel.ScriptFilePath, ViewModel.TranslateFilePath));

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


    private void RefreshContentVisibility()
    {
        foreach (var child in LinePanel.Children)
            switch (child)
            {
                case DialogLine dialogLine:
                    var lineCount = dialogLine.ViewModel.RawContent.Split("\n").Length;
                    dialogLine.Visibility = lineCount switch
                    {
                        1 => ViewModel is { ShowDialog: true, ShowDialogLine1: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        2 => ViewModel is { ShowDialog: true, ShowDialogLine2: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        3 => ViewModel is { ShowDialog: true, ShowDialogLine3: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        _ => dialogLine.Visibility
                    };
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
    private Subject<(int Fps, TimeSpan Eta)>? _fpsChangedSubject;
    private IDisposable? _fpsChangedSubscription;
    private Subject<double>? _progressChangedSubject;
    private IDisposable? _progressChangedSubscription;
    private IDisposable? _subtitlePowerRequest;
    private CancellationTokenSource? TokenSource { get; } = new();
    private CancellationToken CancellationToken => TokenSource!.Token;

    private VideoProcessor? VideoProcessor { get; set; }

    public async void OnNavigatedTo()
    {
        try
        {
            await CheckResource();
            await CheckSavedProgressOnStartup();
        }
        catch (Exception e)
        {
            (Application.Current.MainWindow as MainWindow)?.OnCheckResourceFailed(e, OnNavigatedTo);
        }
    }

    private async Task CheckResource()
    {
        if (await ResourceManager.Instance.CheckResource(ResourceType.VideoProcess)) return;

        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new RefreshWaitDialog("正在刷新下载源数据");
        var source = new CancellationTokenSource();
        _ = dialogService.ShowAsync(dialog, source.Token);
        await ResourceManager.Instance.EnsureResource(ResourceType.VideoProcess);
        await source.CancelAsync();
    }

    private void StopProcess()
    {
        VideoProcessor?.StopProcess();
    }

    private void SetVideoProcessWindowTitle(string status)
    {
        (Application.Current.MainWindow as MainWindow)?.SetWindowTitle(
            $"{status} - {Path.GetFileName(ViewModel.VideoFilePath)}");
    }

    private static void SetTaskbarProgressState(TaskbarItemProgressState state, double value)
    {
        (Application.Current.MainWindow as MainWindow)?.SetTaskbarProgressState(state, value);
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

    private async Task<ContentDialogResult> ShowResumeDialogAsync()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "恢复进度",
                Content = "检测到未完成的处理进度，是否继续？",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消"
            }, CancellationToken);
        if (result == ContentDialogResult.None)
            foreach (var (saveKey, _) in ProgressStore.EnumerateProgressFiles())
                ProgressStore.Delete(saveKey);

        return result;
    }

    private async Task ShowResumeDialogAsync(string saveKey)
    {
        var result = await ShowResumeDialogAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var resumeState = ProgressStore.Load(saveKey);
        if (resumeState == null)
            ProgressStore.Delete(saveKey);

        StartProcess(saveKey, resumeState);
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
                    GetMatchingThreshold()
                ), new VideoProcessCallbacks
                {
                    OnTaskFinished = () =>
                    {
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
                                SetVideoProcessWindowTitle("已取消");
                                SetTaskbarProgressState(TaskbarItemProgressState.Paused,
                                    ProgressBarProgression.Value);
                                Logger.Log("处理已由用户取消，可输出当前结果");
                                SnackService.Show("提示", "处理已取消，可以输出当前结果进行人工复核",
                                    ControlAppearance.Info,
                                    new SymbolIcon(SymbolRegular.Info24), new TimeSpan(0, 0, 4));
                            }
                            else if (stopReason == ProcessStopReason.Completed)
                            {
                                ViewModel.IsFinished = true;
                                ProgressBarProgression.Value = 1;
                                ProgressBarProgression.Maximum = 1;
                                TextBlockProgression.Text = $"{1:P}";
                                SetVideoProcessWindowTitle("已完成");
                                SetTaskbarProgressState(TaskbarItemProgressState.Normal, 1);
                                Logger.Log("处理成功完成");
                                SnackService.Show("成功", "运行结束", ControlAppearance.Success,
                                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
                            }
                            else if (resultReport is { CanExport: true })
                            {
                                ViewModel.IsPartial = true;
                                SetVideoProcessWindowTitle("部分完成");
                                SetTaskbarProgressState(TaskbarItemProgressState.Paused,
                                    ProgressBarProgression.Value);
                                Logger.Log($"处理部分完成: {resultReport.Summary}", LogLevel.Warning);
                                SnackService.Show("警告",
                                    $"处理未完整结束，已识别 {resultReport.RecognizedTotal}/{resultReport.Total} 项，可输出当前结果进行人工复核",
                                    ControlAppearance.Caution,
                                    new SymbolIcon(SymbolRegular.Warning24), new TimeSpan(0, 0, 5));
                            }
                            else
                            {
                                ViewModel.IsFailed = true;
                                SetVideoProcessWindowTitle("处理失败");
                                SetTaskbarProgressState(TaskbarItemProgressState.Error,
                                    ProgressBarProgression.Value);
                                var errorMsg = stopReason switch
                                {
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

                            TextBlockEta.Text = "";
                        });
                    },
                    OnTaskStarted = () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SetVideoProcessWindowTitle("处理中");
                            SetTaskbarProgressState(TaskbarItemProgressState.Normal,
                                ProgressBarProgression.Value);
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
                ProcessingStatePersistence.Instance
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
            SetVideoProcessWindowTitle("处理失败");
            SetTaskbarProgressState(TaskbarItemProgressState.Error, ProgressBarProgression.Value);
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
                    if (!ViewModel.IsRunning) return;

                    ProgressBarProgression.Value = value;
                    ProgressBarProgression.Maximum = 1;
                    TextBlockProgression.Text = $"{value:P}";
                    (Application.Current.MainWindow as MainWindow)?.SetTaskbarProgressValue(value);
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

    private void DialogFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialog = !ViewModel.ShowDialog;
        RefreshContentVisibility();
    }

    private void DialogFilterLine1Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine1 = !ViewModel.ShowDialogLine1;
        RefreshContentVisibility();
        e.Handled = true;
    }

    private void DialogFilterLine2Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine2 = !ViewModel.ShowDialogLine2;
        RefreshContentVisibility();
        e.Handled = true;
    }

    private void DialogFilterLine3Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine3 = !ViewModel.ShowDialogLine3;
        RefreshContentVisibility();
        e.Handled = true;
    }


    private void BannerFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowBanner = !ViewModel.ShowBanner;
        RefreshContentVisibility();
    }

    private void MarkerFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowMarker = !ViewModel.ShowMarker;
        RefreshContentVisibility();
    }

    private void VideoFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.VideoFilePath);
    }

    private void ScriptFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.ScriptFilePath);
    }

    private void TranslateFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.TranslateFilePath);
    }

    private void BackToTopBtn_OnClick(object sender, RoutedEventArgs e)
    {
        LineViewer.ScrollToTop();
    }

    private void PreviewToggleBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowPreview = !ViewModel.ShowPreview;
    }

    private void BackToBottomBtn_OnClick(object sender, RoutedEventArgs e)
    {
        LineViewer.ScrollToBottom();
    }

    private void SubtitlePage_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Z ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.FocusedElement is System.Windows.Controls.TextBox)
            return;

        if (GeneralFunctionSwitch.EventTimeline && EventTimelineEditor.Undo())
            e.Handled = true;
    }
}

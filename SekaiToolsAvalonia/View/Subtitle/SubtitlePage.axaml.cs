using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Emgu.CV;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Utils;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.Services;
using SekaiToolsAvalonia.Utils;
using SekaiToolsAvalonia.ViewModel.Subtitle;
using SekaiToolsAvalonia.ViewModel.Setting;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace SekaiToolsAvalonia.View.Subtitle;

public partial class SubtitlePage : UserControl, IAppPage
{
    public SubtitlePage()
    {
        DataContext = new SubtitlePageModel();
        InitializeComponent();
        SubscribeFpsChange();
        SubscribeProgressChange();
    }

    public SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext!;

    // File selection helpers
    private async Task<string?> SelectFileAsync(string title, string[] extensions)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var filters = extensions.Select(e => new FilePickerFileType(e) { Patterns = [$"*.{e}"] }).ToList();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
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
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (jsonExt.Contains(fileExt))
        {
            ViewModel.ScriptFilePath = filename;
            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (txtExt.Contains(fileExt))
        {
            ViewModel.TranslateFilePath = filename;
            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
        }
    }

    // File browser click handlers
    private async void VideoFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await SelectFileAsync("选择视频文件", ["mp4", "avi", "mkv", "webm", "wmv"]);
        if (result != null) await SelectSameNameFile(result);
    }

    private async void ScriptFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await SelectFileAsync("选择剧本文件", ["json", "asset"]);
        if (result != null) await SelectSameNameFile(result);
    }

    private async void TranslationFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await SelectFileAsync("选择翻译文件", ["txt"]);
        if (result != null) await SelectSameNameFile(result);
    }

    // Control buttons
    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.Reset();
        LinePanel.Children.Clear();
        TextBlockProgression.Text = "";
        TextBlockFps.Text = "";
        ProgressBarProgression.Value = 0;
    }

    private void StopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.IsRunning = false;
        ViewModel.IsFinished = true;
    }

    private async void HistoryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var entries = HistoryStore.LoadAll();
        if (entries.Count == 0)
        {
            ShowMessage("暂无历史记录");
            return;
        }

        // Simple history selection via dialog
        var entryNames = entries.Select(e => $"{e.Timestamp:yyyy-MM-dd HH:mm:ss} - {Path.GetFileName(e.State.VideoFilePath)}").ToList();
        // TODO: Show proper history selection dialog
        ShowMessage($"共 {entries.Count} 条历史记录（选择功能待实现）");
    }

    private async void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!CheckConfig()) return;
            var saveKey = ProgressStore.GetSaveKey(ViewModel.VideoFilePath, ViewModel.ScriptFilePath, ViewModel.TranslateFilePath);
            StartProcess(saveKey, null);
        }
        catch (Exception ex)
        {
            ShowMessage($"启动处理失败: {ex.Message}");
        }
    }

    private bool CheckConfig()
    {
        var vfp = ViewModel.VideoFilePath;
        var sfp = ViewModel.ScriptFilePath;
        var tfp = ViewModel.TranslateFilePath;
        if (string.IsNullOrEmpty(vfp) || string.IsNullOrEmpty(sfp) || string.IsNullOrEmpty(tfp))
        {
            MainWindow.Snackbar?.Show("请填写完整的文件路径");
            return false;
        }
        if (!File.Exists(vfp)) { MainWindow.Snackbar?.Show("视频文件不存在"); return false; }
        if (!File.Exists(sfp)) { MainWindow.Snackbar?.Show("剧情脚本文件不存在"); return false; }
        if (!File.Exists(tfp)) { MainWindow.Snackbar?.Show("剧情翻译文件不存在"); return false; }
        return true;
    }

    // Line management
    private void LinePanel_AddDialogLine(DialogBaseFrameSet set)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var line = new Components.DialogLine(set) { Margin = new Avalonia.Thickness(5, 5, 10, 5) };
            LinePanel.Children.Add(line);
            ViewModel.DialogCurrent++;
            RefreshContentVisibility();
        });
    }

    private void LinePanel_AddBannerLine(BannerBaseFrameSet set)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var line = new Components.BannerLine(set) { Margin = new Avalonia.Thickness(5, 5, 10, 5) };
            LinePanel.Children.Add(line);
            ViewModel.BannerCurrent++;
            RefreshContentVisibility();
        });
    }

    private void LinePanel_AddMarkerLine(MarkerBaseFrameSet set)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var line = new Components.MarkerLine(set) { Margin = new Avalonia.Thickness(5, 5, 10, 5) };
            LinePanel.Children.Add(line);
            ViewModel.MarkerCurrent++;
            RefreshContentVisibility();
        });
    }

    private async void OutputButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var defaultName = Path.GetFileNameWithoutExtension(ViewModel.VideoFilePath) + ".ass";
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存字幕文件",
            DefaultExtension = "ass",
            SuggestedFileName = defaultName,
            FileTypeChoices = [new FilePickerFileType("ASS字幕文件") { Patterns = ["*.ass"] }]
        });

        if (file == null) return;

        try
        {
            var subtitle = GenerateSubtitle();
            await File.WriteAllTextAsync(file.Path.LocalPath, subtitle.ToString(), Encoding.UTF8);

            ProgressStore.Delete(ProgressStore.GetSaveKey(
                ViewModel.VideoFilePath, ViewModel.ScriptFilePath, ViewModel.TranslateFilePath));

            ShowMessage("字幕文件已保存");
        }
        catch (Exception ex)
        {
            ShowMessage($"保存字幕文件失败: {ex.Message}");
        }
    }

    private SekaiToolsBase.SubStationAlpha.Subtitle GenerateSubtitle()
    {
        List<BannerBaseFrameSet> bannerFrameSets = [];
        List<DialogBaseFrameSet> dialogFrameSets = [];
        List<MarkerBaseFrameSet> markerFrameSets = [];
        foreach (var child in LinePanel.Children)
        {
            switch (child)
            {
                case Components.DialogLine dialogLine:
                    var dset = dialogLine.ViewModel.Set;
                    dset.Data.BodyTranslated = dset.Data.BodyTranslated.Replace("…", "...");
                    dialogFrameSets.Add(dset);
                    break;
                case Components.BannerLine bannerLine:
                    bannerFrameSets.Add(bannerLine.ViewModel.Set);
                    break;
                case Components.MarkerLine markerLine:
                    markerFrameSets.Add(markerLine.ViewModel.Set);
                    break;
            }
        }

        return VideoProcessor?.GenerateSubtitle(bannerFrameSets, dialogFrameSets, markerFrameSets)
               ?? throw new NullReferenceException("VideoProcessor is null");
    }

    private void RefreshContentVisibility()
    {
        foreach (var child in LinePanel.Children)
        {
            switch (child)
            {
                case Components.DialogLine dialogLine:
                    dialogLine.IsVisible = ViewModel.ShowDialog;
                    break;
                case Components.BannerLine bannerLine:
                    bannerLine.IsVisible = ViewModel.ShowBanner;
                    break;
                case Components.MarkerLine markerLine:
                    markerLine.IsVisible = ViewModel.ShowMarker;
                    break;
            }
        }
    }

    private void ShowPreviewButton_OnClick(object? sender, RoutedEventArgs e) => ViewModel.ShowPreview = true;

    private static void ShowMessage(string msg) => MainWindow.Snackbar?.Show(msg);
}

// Processing pipeline
public partial class SubtitlePage
{
    private VideoProcessor? VideoProcessor { get; set; }
    private Subject<(int Fps, TimeSpan Eta)>? _fpsChangedSubject;
    private IDisposable? _fpsChangedSubscription;
    private Subject<double>? _progressChangedSubject;
    private IDisposable? _progressChangedSubscription;

    public async void OnNavigatedTo()
    {
        try
        {
            await CheckResource();
            await CheckSavedProgressOnStartup();
        }
        catch (Exception e)
        {
            ShowMessage($"资源检查失败: {e.Message}");
        }
    }

    private async Task CheckResource()
    {
        if (await ResourceManager.Instance.CheckResource(ResourceType.VideoProcess)) return;
        await ResourceManager.Instance.EnsureResource(ResourceType.VideoProcess);
    }

    private void StopProcess()
    {
        VideoProcessor?.StopProcess();
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

            // Auto-resume for now
            ViewModel.VideoFilePath = state.VideoFilePath;
            ViewModel.ScriptFilePath = state.ScriptFilePath;
            ViewModel.TranslateFilePath = state.TranslateFilePath;
            StartProcess(saveKey, state);
            return;
        }
    }

    private void StartProcess(string saveKey, ProcessingState? resumeState)
    {
        var settings = SettingPageModel.Instance;

        Logger.Log($"开始处理: 视频={ViewModel.VideoFilePath}, 剧本={ViewModel.ScriptFilePath}",
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
                    Dispatcher.UIThread.Post(() =>
                    {
                        ViewModel.IsRunning = false;
                        if (VideoProcessor?.Finished ?? false)
                        {
                            ViewModel.IsFinished = true;
                            Logger.Log("处理成功完成", LogLevel.Information);
                        }
                    });
                },
                OnTaskStarted = () => Dispatcher.UIThread.Post(() =>
                {
                    ViewModel.IsRunning = true;
                    ViewModel.HasNotStarted = false;
                    var cl = VideoProcessor?.ContentLength;
                    if (cl != null)
                    {
                        ViewModel.DialogTotal = cl.Dialog; ViewModel.DialogCurrent = 0;
                        ViewModel.BannerTotal = cl.Banner; ViewModel.BannerCurrent = 0;
                        ViewModel.MarkerTotal = cl.Marker; ViewModel.MarkerCurrent = 0;
                    }
                }),
                OnProgress = OnProgressChanged,
                OnFramePreviewImage = frame =>
                {
                    var clone = frame.Clone();
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ViewModel.ShowPreview)
                            ViewModel.FramePreviewImage = MatToBitmap(clone);
                        clone.Dispose();
                    });
                },
                OnNewDialog = LinePanel_AddDialogLine,
                OnNewBanner = LinePanel_AddBannerLine,
                OnNewMarker = LinePanel_AddMarkerLine,
                OnException = e =>
                {
                    Logger.Log($"视频处理异常: {e.Message}\n{e.StackTrace}", LogLevel.Error);
                    Dispatcher.UIThread.Post(() => ViewModel.IsRunning = false);
                },
                OnFps = OnFpsChanged
            });

            if (resumeState != null)
            {
                VideoProcessor.ApplyState(resumeState);
                VideoProcessor.ReplayFinishedCallbacks(LinePanel_AddDialogLine, LinePanel_AddBannerLine, LinePanel_AddMarkerLine);
            }

            VideoProcessor.EnableProgressSaving(saveKey);
            VideoProcessor.StartProcess();
        }
        catch (Exception ex)
        {
            Logger.Log($"初始化视频处理器失败: {ex.Message}", LogLevel.Error);
        }
    }

    private static Bitmap? MatToBitmap(Mat mat) => ImageConvert.MatToBitmap(mat);

    private MatchingThreshold GetMatchingThreshold()
    {
        var thresholdData = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, "thresholds.json");
        if (!File.Exists(thresholdData)) return new MatchingThreshold();
        var json = File.ReadAllText(thresholdData);
        return JsonSerializer.Deserialize<MatchingThreshold>(json);
    }

    private void OnFpsChanged(int fps, TimeSpan eta) => _fpsChangedSubject?.OnNext((fps, eta));
    private void OnProgressChanged(double progression) => _progressChangedSubject?.OnNext(progression);

    private void SubscribeFpsChange()
    {
        _fpsChangedSubscription?.Dispose();
        _fpsChangedSubject?.OnCompleted();
        _fpsChangedSubject?.Dispose();
        _fpsChangedSubject = new Subject<(int, TimeSpan)>();
        _fpsChangedSubscription = _fpsChangedSubject
            ?.Sample(TimeSpan.FromMilliseconds(200))
            .Subscribe(x =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    TextBlockFps.Text = $"FPS: {x.Item1}";
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
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressBarProgression.Value = value;
                    ProgressBarProgression.Maximum = 1;
                    TextBlockProgression.Text = $"{value:P}";
                });
            });
    }
}

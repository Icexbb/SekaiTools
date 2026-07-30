using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.SubStationAlpha;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Process.Performance;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SekaiToolsCore;

public class VideoProcessCallbacks
{
    public Action OnTaskStarted { get; set; } = () => { };
    public Action OnTaskFinished { get; set; } = () => { };
    public Action<Mat> OnFramePreviewImage { get; set; } = mat => { };

    public Action<DialogBaseFrameSet> OnNewDialog { get; set; } = dialog => { };

    public Action<BannerBaseFrameSet> OnNewBanner { get; set; } = banner => { };

    public Action<MarkerBaseFrameSet> OnNewMarker { get; set; } = marker => { };

    public Action<Exception> OnException { get; set; } = e => { };

    public Action<double> OnProgress { get; set; } = progress => { };

    public Action<int, TimeSpan> OnFps { get; set; } = (fps, eta) => { };
}

public record ContentLength(int Dialog, int Banner, int Marker);

/// <summary>
///     视频处理停止原因
/// </summary>
public enum ProcessStopReason
{
    None, // 未停止或初始状态
    Completed, // 正常完成
    Canceled, // 用户取消
    EndOfStream, // 正常到达视频末尾，但仍有未完成目标
    ReadFailed, // 读帧失败
    ExceptionThreshold, // 异常计数超过阈值
    CaptureError, // 捕获设备错误
    UnexpectedError // 未处理异常
}

public class VideoProcessor : IDisposable
{
    private const int ExceptionThreshold = 10;
    private const int MaxReadRetries = 2;
    private const long CallbackThrottleMs = 200;
    private readonly object _progressSaveLock = new();
    private readonly Config _config;
    private readonly ProcessingPerformanceMetrics _performanceMetrics = new();
    private readonly ProcessingStateMetadata _stateMetadata;
    private readonly int _saveInterval = 300;
    private readonly string _scriptPath;
    private readonly string _translatePath;
    private readonly string _videoPath;
    private int _consecutiveExceptionCount;
    private bool _disposed;
    private bool _frameSetJustCompleted;
    private int _framesSinceLastSave;
    private volatile bool _isProcessing;
    private long _lastFpsCallbackTime;

    // 回调节流
    private long _lastProgressCallbackTime;

    // 预览图像有界队列（长度 1，只保留最新帧）
    private Channel<Mat>? _previewChannel;
    private Task? _previewConsumerTask;
    private Task _progressSaveTask = Task.CompletedTask;

    // 进度保存
    private string? _saveKey;

    public VideoProcessor(Config config, VideoProcessCallbacks callbacks)
    {
        _config = config;
        _videoPath = config.VideoFilePath;
        _scriptPath = config.ScriptFilePath;
        _translatePath = config.TranslateFilePath;
        Creator = new TemplateMatcherCreator(config);
        Capture = new VideoCapture(config.VideoFilePath);
        DialogMatcher = Creator.DialogMatcher();
        ContentMatcher = Creator.ContentMatcher();
        BannerMatcher = Creator.BannerMatcher();
        MarkerMatcher = Creator.MarkerMatcher();
        _stateMetadata = ProcessingStateMetadata.Create(config,
            VideoStateMetadata.From(Creator.VideoInfo));
        Callbacks = callbacks;
    }

    // 处理结果
    public ProcessStopReason StopReason { get; private set; } = ProcessStopReason.None;

    private CancellationTokenSource? TokenSource { get; set; } = new();
    private ContentTemplateMatcher? ContentMatcher { get; }

    private DialogTemplateMatcher? DialogMatcher { get; }
    private MarkerTemplateMatcher? MarkerMatcher { get; }
    private BannerTemplateMatcher? BannerMatcher { get; }

    private TemplateMatcherCreator? Creator { get; }
    private Task? ProcessingTask { get; set; }
    private VideoCapture? Capture { get; set; }

    private VideoProcessCallbacks Callbacks { get; }


    public bool Finished => ContentMatcher is { Finished: true } &&
                            DialogMatcher is { Finished: true } &&
                            BannerMatcher is { Finished: true } &&
                            MarkerMatcher is { Finished: true };

    public ContentLength ContentLength => new(
        DialogMatcher?.Set.Count ?? 0,
        BannerMatcher?.Set.Count ?? 0,
        MarkerMatcher?.Set.Count ?? 0
    );

    public ProcessingPerformanceSnapshot Performance => _performanceMetrics.Snapshot();

    public IReadOnlyList<MatcherDiagnostic> Diagnostics =>
        DialogMatcher?.Diagnostics
            .Concat(BannerMatcher?.Diagnostics ?? [])
            .Concat(MarkerMatcher?.Diagnostics ?? [])
            .ToList() ?? [];

    public ProcessingResultReport ResultReport => ProcessingResultReport.Create(
        StopReason,
        DialogMatcher?.Set ?? [],
        BannerMatcher?.Set ?? [],
        MarkerMatcher?.Set ?? [],
        Diagnostics);

    public void Dispose()
    {
        if (_disposed) return;
        if (_isProcessing)
            throw new InvalidOperationException("视频处理仍在运行，不能释放处理器");

        TokenSource?.Dispose();
        Capture?.Dispose();
        ContentMatcher?.Dispose();
        DialogMatcher?.Dispose();
        BannerMatcher?.Dispose();
        MarkerMatcher?.Dispose();
        Creator?.Dispose();
        _disposed = true;
    }

    public Subtitle GenerateSubtitle(List<BannerBaseFrameSet> bannerFrameSets, List<DialogBaseFrameSet> dialogFrameSets,
        List<MarkerBaseFrameSet> markerFrameSets)
    {
        if (Creator == null) throw new NullReferenceException();
        var exportableDialogs = dialogFrameSets.Where(item => !item.IsEmpty()).ToList();
        var exportableBanners = bannerFrameSets.Where(item => !item.IsEmpty()).ToList();
        var exportableMarkers = markerFrameSets.Where(item => !item.IsEmpty()).ToList();
        if (exportableDialogs.Count + exportableBanners.Count + exportableMarkers.Count == 0)
            throw new InvalidOperationException("没有可导出的识别结果");

        var maker = Creator.SubtitleMaker();
        var exportInfo = new SubtitleExportInfo(
            "SekaiTools 自动轴机",
            GetProgramVersion(),
            GetTaskStatus(),
            Path.GetFileName(_videoPath),
            Path.GetFileName(_scriptPath),
            Path.GetFileName(_translatePath),
            ResultReport);
        return maker.Make(exportableDialogs, exportableBanners, exportableMarkers, exportInfo);
    }

    private string GetTaskStatus()
    {
        if (_isProcessing) return "运行中";
        return StopReason switch
        {
            ProcessStopReason.None => "未开始",
            ProcessStopReason.Completed => "已完成",
            ProcessStopReason.Canceled => "已取消",
            ProcessStopReason.EndOfStream => "视频已结束，存在未完成识别目标",
            ProcessStopReason.ReadFailed => "视频读帧失败",
            ProcessStopReason.ExceptionThreshold => "异常过多，自动中止",
            ProcessStopReason.CaptureError => "视频捕获设备出错",
            ProcessStopReason.UnexpectedError => "发生未预期错误",
            _ => StopReason.ToString()
        };
    }

    private static string GetProgramVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(VideoProcessor).Assembly;
        return assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
               ?? assembly.GetName().Version?.ToString()
               ?? "未知";
    }

    public void EnableProgressSaving(string saveKey)
    {
        _saveKey = saveKey;
    }

    public ProcessingState CaptureState()
    {
        return new ProcessingState
        {
            Version = ProcessingStateCompatibility.CurrentVersion,
            Metadata = _stateMetadata,
            StopReason = StopReason,
            FrameIndex = GetCurrentFrameIndex(),
            ContentFinished = ContentMatcher?.Finished ?? false,
            VideoFilePath = _videoPath,
            ScriptFilePath = _scriptPath,
            TranslateFilePath = _translatePath,
            Timecodes = Creator?.FrameRate.ExportTimecodes().ToList() ?? [],
            Dialog = DialogMatcher?.SaveState(),
            Banner = BannerMatcher?.SaveState(),
            Marker = MarkerMatcher?.SaveState()
        };
    }

    public void ApplyState(ProcessingState state)
    {
        var compatibility = ProcessingStateCompatibility.ValidateAndMigrate(state, _config, _stateMetadata);
        if (!compatibility.CanRestore)
            throw new InvalidDataException($"无法恢复处理进度: {compatibility.Message}");
        if (compatibility.Status == ProcessingStateCompatibilityStatus.MigratedLegacy)
            Logger.Log(compatibility.Message, ExtLogLevel.Warning);

        StopReason = state.StopReason;

        if (state.Timecodes.Count > 0)
            Creator?.FrameRate.RestoreTimecodes(state.Timecodes);

        if (Capture != null && Capture.Ptr != IntPtr.Zero &&
            !Capture.Set(CapProp.PosFrames, state.FrameIndex))
            throw new InvalidDataException($"视频无法定位到进度帧 {state.FrameIndex}");

        if (state.ContentFinished)
            ContentMatcher?.ForceFinish();

        if (state.Dialog != null)
            DialogMatcher?.RestoreState(state.Dialog);
        if (state.Banner != null)
            BannerMatcher?.RestoreState(state.Banner);
        if (state.Marker != null)
            MarkerMatcher?.RestoreState(state.Marker);

        _framesSinceLastSave = 0;
    }

    public void ReplayFinishedCallbacks(
        Action<DialogBaseFrameSet> onDialog,
        Action<BannerBaseFrameSet> onBanner,
        Action<MarkerBaseFrameSet> onMarker)
    {
        if (DialogMatcher != null)
            foreach (var d in DialogMatcher.Set.Where(d => d.Finished))
                onDialog(d);
        if (BannerMatcher != null)
            foreach (var b in BannerMatcher.Set.Where(b => b.Finished))
                onBanner(b);
        if (MarkerMatcher != null)
            foreach (var m in MarkerMatcher.Set.Where(m => m.Finished))
                onMarker(m);
    }

    public void ReplayExportableCallbacks(
        Action<DialogBaseFrameSet> onDialog,
        Action<BannerBaseFrameSet> onBanner,
        Action<MarkerBaseFrameSet> onMarker)
    {
        if (DialogMatcher != null)
            foreach (var item in DialogMatcher.Set.Where(item => !item.IsEmpty()))
                onDialog(item);
        if (BannerMatcher != null)
            foreach (var item in BannerMatcher.Set.Where(item => !item.IsEmpty()))
                onBanner(item);
        if (MarkerMatcher != null)
            foreach (var item in MarkerMatcher.Set.Where(item => !item.IsEmpty()))
                onMarker(item);
    }

    private int GetCurrentFrameIndex()
    {
        if (Capture == null || Capture.Ptr == IntPtr.Zero) return 0;
        return (int)Capture.Get(CapProp.PosFrames);
    }

    public void StartProcess()
    {
        if (ProcessingTask is { IsCompleted: false }) return;

        // 防止并发启动
        if (_isProcessing) return;

        TokenSource?.Dispose();
        TokenSource = new CancellationTokenSource();
        var token = TokenSource.Token;

        _isProcessing = true;
        StopReason = ProcessStopReason.None;
        _consecutiveExceptionCount = 0;
        _lastProgressCallbackTime = 0;
        _lastFpsCallbackTime = 0;
        _performanceMetrics.Reset();

        var cap = Capture;
        if (cap != null)
            Logger.Log(
                $"开始视频处理: {(int)cap.Get(CapProp.FrameWidth)}x{(int)cap.Get(CapProp.FrameHeight)}, {(int)cap.Get(CapProp.FrameCount)}帧, {cap.Get(CapProp.Fps):F2}fps");
        ProcessingTask = Task.Run(() =>
        {
            try
            {
                Callbacks.OnTaskStarted();
                Process(token);
            }
            catch (OperationCanceledException)
            {
                StopReason = ProcessStopReason.Canceled;
            }
            catch (Exception e)
            {
                StopReason = ProcessStopReason.UnexpectedError;

                TokenSource?.Cancel();
                TeardownPreview();
                Callbacks.OnException(e);
            }
            finally
            {
                _isProcessing = false;
                Callbacks.OnTaskFinished();
            }
        });
    }

    public void StopProcess()
    {
        TokenSource?.Cancel();
    }

    private void Process(CancellationToken token)
    {
        if (Capture == null || Capture.Ptr == IntPtr.Zero ||
            DialogMatcher == null || ContentMatcher == null ||
            BannerMatcher == null || MarkerMatcher == null)
        {
            StopReason = ProcessStopReason.CaptureError;
            return;
        }

        var capture = Capture;
        var frameRate = capture.Get(CapProp.Fps);
        var previewInterval = Math.Max(1, (int)Math.Round(frameRate / 5d));
        var frame = new Mat();
        using var matchFrameA = new FrameMatchContext();
        using var matchFrameB = new FrameMatchContext();
        FrameMatchContext? previousMatchFrame = null;
        var previousMatchFrameIndex = -1;
        var useFirstMatchFrame = true;
        if (Creator == null) throw new NullReferenceException();
        var frameCount = capture.Get(CapProp.FrameCount);
        var markerIndexInDialog = MarkerIndexOfDialog();

        // 初始化预览通道（有界队列，容量 1）
        _previewChannel = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // 启动预览消费任务
        _previewConsumerTask = StartPreviewConsumer(_previewChannel, token);

        ApplyDebugConfig(capture, DialogMatcher);

        var avgDuration = 0d;
        var frameIndex = 0;
        var readRetryCount = 0;
        while (true)
        {
            var tic = Environment.TickCount;
            long matchingStart = 0;
            try
            {
                if (token.IsCancellationRequested)
                {
                    StopReason = ProcessStopReason.Canceled;
                    break;
                }

                if (capture is not { IsOpened: true })
                {
                    StopReason = ProcessStopReason.CaptureError;
                    break;
                }

                var decodeStart = Stopwatch.GetTimestamp();
                var readSucceeded = capture.Read(frame);
                _performanceMetrics.Record(ProcessingStage.Decode, Stopwatch.GetElapsedTime(decodeStart));
                if (!readSucceeded)
                {
                    var action = VideoReadFailureClassifier.Classify(
                        capture.IsOpened,
                        capture.Get(CapProp.PosFrames),
                        frameCount,
                        readRetryCount,
                        MaxReadRetries);
                    switch (action)
                    {
                        case VideoReadFailureAction.Retry:
                            readRetryCount++;
                            Logger.Log($"视频读帧暂时失败，正在重试 ({readRetryCount}/{MaxReadRetries})",
                                ExtLogLevel.Warning);
                            continue;
                        case VideoReadFailureAction.EndOfStream:
                            StopReason = ProcessStopReason.EndOfStream;
                            break;
                        case VideoReadFailureAction.CaptureError:
                            StopReason = ProcessStopReason.CaptureError;
                            break;
                        case VideoReadFailureAction.ReadFailed:
                        default:
                            StopReason = ProcessStopReason.ReadFailed;
                            break;
                    }
                    break;
                }
                readRetryCount = 0;

                frameIndex = (int)capture.Get(CapProp.PosFrames);
                Creator.FrameRate.RecordTimecode(Math.Max(0, frameIndex - 1), capture.Get(CapProp.PosMsec));
                Creator.CachePool.SetFrameIndex(frameIndex);
                var preprocessStart = Stopwatch.GetTimestamp();
                var matchFrame = useFirstMatchFrame ? matchFrameA : matchFrameB;
                var backcheckFrame = previousMatchFrame;
                var backcheckFrameIndex = previousMatchFrameIndex;
                matchFrame.Update(frame);
                useFirstMatchFrame = !useFirstMatchFrame;
                previousMatchFrame = matchFrame;
                previousMatchFrameIndex = frameIndex;
                _performanceMetrics.Record(ProcessingStage.Preprocess, Stopwatch.GetElapsedTime(preprocessStart));
                _performanceMetrics.RecordFrame();
                var progress = frameCount > 0 ? frameIndex / frameCount : 0;

                // 节流进度回调（200ms）
                EmitProgressIfNeeded(progress);

                if (frameIndex % previewInterval == 0)
                {
                    var previewFrame = frame.Clone();
                    EnqueueLatestPreview(previewFrame);
                }

                matchingStart = Stopwatch.GetTimestamp();
                if (ContentMatcher is { Finished: false })
                {
                    ContentMatcher.Process(matchFrame);
                    continue;
                }

                var matchBannerNow = true;
                if (DialogMatcher is { Finished: false })
                {
                    var dialogIndex = DialogMatcher.LastNotProcessedIndex();
                    var r = DialogMatcher.Process(matchFrame, frameIndex, backcheckFrame, backcheckFrameIndex);
                    matchBannerNow = !r;
                    if (DialogMatcher.Set[dialogIndex].Finished)
                    {
                        Callbacks.OnNewDialog(DialogMatcher.Set[dialogIndex]);
                        _frameSetJustCompleted = true;
                    }
                }
                else if (BannerMatcher is { Finished: true } && MarkerMatcher is { Finished: true })
                {
                    break;
                }

                if (BannerMatcher is { Finished: false } && matchBannerNow)
                {
                    var bannerIndex = BannerMatcher.LastNotProcessedIndex();
                    BannerMatcher.Process(matchFrame, frameIndex, backcheckFrame, backcheckFrameIndex);
                    if (BannerMatcher.Set[bannerIndex].Finished)
                    {
                        Callbacks.OnNewBanner(BannerMatcher.Set[bannerIndex]);
                        _frameSetJustCompleted = true;
                    }
                }

                if (MarkerMatcher is { Finished: false } && MatchMarkerNow())
                {
                    var markerIndex = MarkerMatcher.LastNotProcessedIndex();
                    MarkerMatcher.Process(matchFrame, frameIndex, backcheckFrame, backcheckFrameIndex);
                    if (MarkerMatcher.Set[markerIndex].Finished)
                    {
                        Callbacks.OnNewMarker(MarkerMatcher.Set[markerIndex]);
                        _frameSetJustCompleted = true;
                    }
                }

                _performanceMetrics.Record(ProcessingStage.Match, Stopwatch.GetElapsedTime(matchingStart));
                matchingStart = 0;

                // 清空异常计数（处理成功）
                _consecutiveExceptionCount = 0;

                // 定期保存进度
                TrySaveProgress();
            }
            catch (OperationCanceledException)
            {
                StopReason = ProcessStopReason.Canceled;
                break;
            }
            catch (Exception e)
            {
                // 异常熔断：连续异常超过阈值则退出
                _consecutiveExceptionCount++;
                if (_consecutiveExceptionCount >= ExceptionThreshold)
                {
                    StopReason = ProcessStopReason.ExceptionThreshold;
                    if (Debugger.IsAttached) throw;
                    else Callbacks.OnException(new AggregateException($"连续异常 {ExceptionThreshold} 次，已中止处理", e));
                    break;
                }

                if (Debugger.IsAttached) throw;
                else Callbacks.OnException(e);
            }
            finally
            {
                if (matchingStart != 0)
                    _performanceMetrics.Record(ProcessingStage.Match, Stopwatch.GetElapsedTime(matchingStart));
                var toc = Environment.TickCount;
                Fps(toc - tic);
            }
        }

        // 循环正常退出代表所有匹配器均已完成。先确定终止状态，再发送最终进度，
        // 避免取消或失败任务被错误显示为 100%。
        if (StopReason == ProcessStopReason.None)
            StopReason = ProcessStopReason.Completed;

        var finalProgress = frameCount > 0 ? Math.Clamp(frameIndex / frameCount, 0, 1) : 0;
        Callbacks.OnProgress(StopReason == ProcessStopReason.Completed ? 1 : finalProgress);

        TeardownPreview();

        if (StopReason != ProcessStopReason.Completed)
        {
            if (DialogMatcher.Set.FirstOrDefault(item => !item.Finished && !item.IsEmpty()) is { } dialog)
                Callbacks.OnNewDialog(dialog);
            if (BannerMatcher.Set.FirstOrDefault(item => !item.Finished && !item.IsEmpty()) is { } banner)
                Callbacks.OnNewBanner(banner);
            if (MarkerMatcher.Set.FirstOrDefault(item => !item.Finished && !item.IsEmpty()) is { } marker)
                Callbacks.OnNewMarker(marker);
        }

        var finalState = CaptureState();
        if (_saveKey != null)
            QueueProgressSave(_saveKey, finalState);
        WaitForProgressSave();

        frame.Dispose();
        capture.Dispose();
        if (ReferenceEquals(Capture, capture))
            Capture = null;

        Logger.Log($"视频处理结束: {StopReason}, 当前帧={frameIndex}, 总帧={frameCount}");
        Logger.Log($"视频处理性能: {Performance}");
        foreach (var diagnostic in Diagnostics)
            Logger.Log($"匹配诊断: {diagnostic.Matcher}[{diagnostic.TargetIndex}] " +
                       $"帧={diagnostic.FrameIndex}, {diagnostic.Reason}", ExtLogLevel.Warning);

        if (ResultReport.CanExport)
            HistoryStore.Add(finalState);

        return;

        void TrySaveProgress()
        {
            if (_saveKey == null) return;
            _framesSinceLastSave++;

            if (_framesSinceLastSave >= _saveInterval || _frameSetJustCompleted)
            {
                _framesSinceLastSave = 0;
                _frameSetJustCompleted = false;
                var snapshot = CaptureState();
                var key = _saveKey;
                QueueProgressSave(key, snapshot);
            }
        }

        bool MatchMarkerNow()
        {
            if (MarkerMatcher!.Set.Count == 0) return false;
            var markerIndex = MarkerMatcher!.LastNotProcessedIndex();
            var dialogIndex = DialogMatcher!.LastNotProcessedIndex();
            if (dialogIndex < 0) return true;
            return dialogIndex >= markerIndexInDialog[markerIndex];
        }


        List<int> MarkerIndexOfDialog()
        {
            var dialogCount = -1;
            var markerIndex = new List<int>();
            var events = new Queue<BaseStoryEvent>(
                Creator!.Story.GetTypes(Story.StoryEventType.Dialog | Story.StoryEventType.Marker)
            );
            while (events.TryDequeue(out var ev))
                switch (ev)
                {
                    case DialogStoryEvent:
                        dialogCount += 1;
                        break;
                    case MarkerStoryEvent:
                        markerIndex.Add(dialogCount);
                        break;
                }

            return markerIndex.Select(x => x < 0 ? 0 : x).ToList();
        }

        void EmitProgressIfNeeded(double progress)
        {
            var now = Environment.TickCount64;
            if (now - _lastProgressCallbackTime < CallbackThrottleMs) return;
            Callbacks.OnProgress(progress);
            _lastProgressCallbackTime = now;
        }

        void Fps(int deltaTime)
        {
            const double alpha = 1d / 100d; // 采样数设置为100

            avgDuration = avgDuration <= double.Epsilon
                ? deltaTime
                : avgDuration * (1 - alpha) + deltaTime * alpha;

            var now = Environment.TickCount64;
            if (now - _lastFpsCallbackTime >= CallbackThrottleMs)
            {
                var fps = avgDuration > double.Epsilon ? (int)(1000d / avgDuration) : 0;
                var etaMs = Math.Max(0, (frameCount - frameIndex) * avgDuration);
                var eta = new TimeSpan(0, 0, 0, 0, (int)etaMs);
                Callbacks.OnFps(fps, eta);
                _lastFpsCallbackTime = now;
            }
        }
    }

    private void QueueProgressSave(string saveKey, ProcessingState state)
    {
        lock (_progressSaveLock)
        {
            _progressSaveTask = SaveAfterPreviousAsync(_progressSaveTask, saveKey, state);
        }
    }

    private static async Task SaveAfterPreviousAsync(Task previousSave, string saveKey, ProcessingState state)
    {
        try
        {
            await previousSave.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.Log($"保存上一份处理进度失败: {e.Message}", ExtLogLevel.Error);
        }

        await Task.Run(() => ProgressStore.Save(saveKey, state)).ConfigureAwait(false);
    }

    private void WaitForProgressSave()
    {
        Task saveTask;
        lock (_progressSaveLock)
        {
            saveTask = _progressSaveTask;
        }

        try
        {
            saveTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Logger.Log($"保存最终处理进度失败: {e.Message}", ExtLogLevel.Error);
        }
    }

    [Conditional("DEBUG")]
    private static void ApplyDebugConfig(VideoCapture capture, DialogTemplateMatcher dialogMatcher)
    {
        if (!Debugger.IsAttached) return;

        if (!int.TryParse(Environment.GetEnvironmentVariable("DebugFrameID"), out var debugFrameId))
            return;

        var targetString = Environment.GetEnvironmentVariable("DebugTargetString");
        var speakerString = Environment.GetEnvironmentVariable("DebugTargetSpeaker");
        if (targetString != null)
        {
            var debugEarlyTerminate = dialogMatcher.DebugSetFinishedUntilContains(targetString, speakerString);
            if (int.TryParse(Environment.GetEnvironmentVariable("DebugEarlyTermination"), out var etLength))
            {
                debugEarlyTerminate += etLength;
                dialogMatcher.DebugSetFinishedAfter(debugEarlyTerminate);
            }
        }

        capture.Set(CapProp.PosFrames, debugFrameId);
    }

    private void TeardownPreview()
    {
        _previewChannel?.Writer.Complete();
        try
        {
            _previewConsumerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 超时或异常忽略
        }

        // 消费任务可能因取消而提前退出，释放仍留在通道中的原生图像。
        while (_previewChannel?.Reader.TryRead(out var pendingFrame) == true)
            pendingFrame.Dispose();
    }

    private void EnqueueLatestPreview(Mat previewFrame)
    {
        var channel = _previewChannel;
        if (channel == null)
        {
            previewFrame.Dispose();
            return;
        }

        if (channel.Writer.TryWrite(previewFrame)) return;

        // 通道容量为 1。显式取出并释放旧帧，避免 DropOldest 静默丢弃 Mat
        // 后无人负责释放其原生缓冲区。
        if (channel.Reader.TryRead(out var droppedFrame))
            droppedFrame.Dispose();

        if (!channel.Writer.TryWrite(previewFrame))
            previewFrame.Dispose();
    }

    private async Task StartPreviewConsumer(Channel<Mat> previewChannel, CancellationToken token)
    {
        try
        {
            await foreach (var frame in previewChannel.Reader.ReadAllAsync(token))
                try
                {
                    Callbacks.OnFramePreviewImage(frame);
                }
                finally
                {
                    frame.Dispose();
                }
        }
        catch (OperationCanceledException)
        {
            // 预期的取消
        }
    }
}

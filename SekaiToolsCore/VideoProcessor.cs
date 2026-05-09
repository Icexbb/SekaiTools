using System.Diagnostics;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.SubStationAlpha;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Utils;

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
/// 视频处理停止原因
/// </summary>
public enum ProcessStopReason
{
    None, // 未停止或初始状态
    Completed, // 正常完成
    Canceled, // 用户取消
    ReadFailed, // 读帧失败
    ExceptionThreshold, // 异常计数超过阈值
    CaptureError // 捕获设备错误
}

public class VideoProcessor
{
    private bool _debugIgnoreBannerMarker;
    private volatile bool _isProcessing;
    private int _consecutiveExceptionCount;
    private const int ExceptionThreshold = 10;

    // 预览图像有界队列（长度 1，只保留最新帧）
    private Channel<Mat>? _previewChannel;
    private Task? _previewConsumerTask;

    // 回调节流
    private long _lastProgressCallbackTime;
    private long _lastFpsCallbackTime;
    private const long CallbackThrottleMs = 200;

    // 处理结果
    public ProcessStopReason StopReason { get; private set; } = ProcessStopReason.None;

    public VideoProcessor(Config config, VideoProcessCallbacks callbacks)
    {
        Creator = new TemplateMatcherCreator(config);
        Capture = new VideoCapture(config.VideoFilePath);
        DialogMatcher = Creator.DialogMatcher();
        ContentMatcher = Creator.ContentMatcher();
        BannerMatcher = Creator.BannerMatcher();
        MarkerMatcher = Creator.MarkerMatcher();
        Callbacks = callbacks;
    }

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

    public Subtitle GenerateSubtitle(List<BannerBaseFrameSet> bannerFrameSets, List<DialogBaseFrameSet> dialogFrameSets,
        List<MarkerBaseFrameSet> markerFrameSets)
    {
        if (Creator == null) throw new NullReferenceException();
        var maker = Creator.SubtitleMaker();
        return maker.Make(dialogFrameSets, bannerFrameSets, markerFrameSets);
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

        ProcessingTask = Task.Run(() =>
        {
            Callbacks.OnTaskStarted();
            try
            {
                Process(token);
            }
            finally
            {
                _isProcessing = false;
                Callbacks.OnTaskFinished();
            }
        }, token);
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
        if (Creator == null) throw new NullReferenceException();
        var frameCount = capture.Get(CapProp.FrameCount);
        var markerIndexInDialog = MarkerIndexOfDialog();

        // 初始化预览通道（有界队列，容量 1）
        _previewChannel = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // 启动预览消费任务
        _previewConsumerTask = StartPreviewConsumer(_previewChannel, token);

        // Debug usage
        if (int.TryParse(Environment.GetEnvironmentVariable("DebugFrameID"), out var debugFrameId))
        {
            var targetString = Environment.GetEnvironmentVariable("DebugTargetString");
            var speakerString = Environment.GetEnvironmentVariable("DebugTargetSpeaker");
            if (targetString != null)
            {
                var debugEarlyTerminate = DialogMatcher.DebugSetFinishedUntilContains(targetString, speakerString);

                if (int.TryParse(Environment.GetEnvironmentVariable("DebugEarlyTermination"), out var etLength))
                {
                    debugEarlyTerminate += etLength;
                    DialogMatcher.DebugSetFinishedAfter(debugEarlyTerminate);
                }
            }

            capture.Set(CapProp.PosFrames, debugFrameId);
        }

        _debugIgnoreBannerMarker = Environment.GetEnvironmentVariable("DebugIgnoreBannerMarker") == "true";

        var avgDuration = 0d;
        var frameIndex = 0;
        while (true)
        {
            var tic = Environment.TickCount;
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

                if (!capture.Read(frame))
                {
                    StopReason = ProcessStopReason.ReadFailed;
                    break;
                }

                frameIndex = (int)capture.Get(CapProp.PosFrames);
                var progress = frameCount > 0 ? frameIndex / frameCount : 0;

                // 节流进度回调（200ms）
                EmitProgressIfNeeded(progress);

                if (frameIndex % previewInterval == 0)
                {
                    var previewFrame = frame.Clone();
                    // 发送预览帧到有界队列（丢旧帧）
                    _ = _previewChannel?.Writer.TryWrite(previewFrame);
                }

                FrameProcess.Process(frame);

                if (ContentMatcher is { Finished: false })
                {
                    ContentMatcher.Process(frame);
                    continue;
                }

                var matchBannerNow = true;
                if (DialogMatcher is { Finished: false })
                {
                    var dialogIndex = DialogMatcher.LastNotProcessedIndex();
                    var r = DialogMatcher.Process(frame, frameIndex);
                    matchBannerNow = !r;
                    if (DialogMatcher.Set[dialogIndex].Finished) Callbacks.OnNewDialog(DialogMatcher.Set[dialogIndex]);
                }
                else if (_debugIgnoreBannerMarker)
                {
                    break;
                }

                if (BannerMatcher is { Finished: false } && matchBannerNow)
                {
                    var bannerIndex = BannerMatcher.LastNotProcessedIndex();
                    BannerMatcher.Process(frame, frameIndex);
                    if (BannerMatcher.Set[bannerIndex].Finished) Callbacks.OnNewBanner(BannerMatcher.Set[bannerIndex]);
                }

                if (MarkerMatcher is { Finished: false } && MatchMarkerNow())
                {
                    var markerIndex = MarkerMatcher.LastNotProcessedIndex();
                    MarkerMatcher.Process(frame, frameIndex);
                    if (MarkerMatcher.Set[markerIndex].Finished) Callbacks.OnNewMarker(MarkerMatcher.Set[markerIndex]);
                }

                // 清空异常计数（处理成功）
                _consecutiveExceptionCount = 0;
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
                var toc = Environment.TickCount;
                Fps(toc - tic);
            }
        }

        EmitProgressIfNeeded(1); // 最终完成信号

        // 关闭预览通道并等待消费任务结束
        _previewChannel?.Writer.Complete();
        try
        {
            _previewConsumerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 超时或异常忽略
        }

        frame.Dispose();
        capture.Dispose();
        if (ReferenceEquals(Capture, capture))
            Capture = null;

        // 如果还未设置停止原因，则标记为完成
        if (StopReason == ProcessStopReason.None)
            StopReason = ProcessStopReason.Completed;

        return;

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

    /// <summary>
    /// 启动预览帧消费任务，确保 Mat 资源正确释放
    /// </summary>
    private async Task StartPreviewConsumer(Channel<Mat> previewChannel, CancellationToken token)
    {
        try
        {
            await foreach (var frame in previewChannel.Reader.ReadAllAsync(token))
            {
                try
                {
                    Callbacks.OnFramePreviewImage(frame);
                }
                finally
                {
                    frame.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 预期的取消
        }
    }
}
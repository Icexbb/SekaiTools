using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Story.Event;
using SekaiToolsCore.SubStationAlpha;
using SekaiToolsCore.Utils;
using Event = SekaiToolsCore.Story.Event.Event;

namespace SekaiToolsCore;

public class VideoProcessCallbacks
{
    public Action OnTaskStarted { get; set; } = () => { };
    public Action OnTaskFinished { get; set; } = () => { };
    public Action<Mat> OnFramePreviewImage { get; set; } = mat => { };

    public Action<DialogFrameSet> OnNewDialog { get; set; } = dialog => { };

    public Action<BannerFrameSet> OnNewBanner { get; set; } = banner => { };

    public Action<MarkerFrameSet> OnNewMarker { get; set; } = marker => { };

    public Action<Exception> OnException { get; set; } = e => { };

    public Action<double> OnProgress { get; set; } = progress => { };

    public Action<int, TimeSpan> OnFps { get; set; } = (fps, eta) => { };
}

public class VideoProcessor
{
    private CancellationTokenSource? TokenSource { get; set; } = new();
    private CancellationToken Token => TokenSource!.Token;
    private ContentMatcher? ContentMatcher { get; set; }

    private DialogMatcher? DialogMatcher { get; set; }
    private MarkerMatcher? MarkerMatcher { get; set; }
    private BannerMatcher? BannerMatcher { get; set; }

    private MatcherCreator? Creator { get; set; }
    private Task? Task { get; set; }
    private VideoCapture? Capture { get; set; }

    private VideoProcessCallbacks Callbacks { get; set; }


    public bool Finished => ContentMatcher is { Finished: true } &&
                            DialogMatcher is { Finished: true } &&
                            BannerMatcher is { Finished: true } &&
                            MarkerMatcher is { Finished: true };

    public Subtitle GenerateSubtitle(List<BannerFrameSet> bannerFrameSets, List<DialogFrameSet> dialogFrameSets,
        List<MarkerFrameSet> markerFrameSets)
    {
        if (Creator == null) throw new NullReferenceException();
        var maker = Creator.SubtitleMaker();
        return maker.Make(dialogFrameSets, bannerFrameSets, markerFrameSets);
    }

    public VideoProcessor(Config config, VideoProcessCallbacks callbacks)
    {
        Creator = new MatcherCreator(config);
        Capture = new VideoCapture(config.VideoFilePath);
        DialogMatcher = Creator.DialogMatcher();
        ContentMatcher = Creator.ContentMatcher();
        BannerMatcher = Creator.BannerMatcher();
        MarkerMatcher = Creator.MarkerMatcher();
        Callbacks = callbacks;
    }

    public void StartProcess()
    {
        Task = new Task(() =>
        {
            Callbacks.OnTaskStarted();
            Process();
            Callbacks.OnTaskFinished();
        }, Token);
        Task.Start();
    }

    public void StopProcess()
    {
        TokenSource?.Cancel();
        TokenSource = new CancellationTokenSource();
        Task = null;

        Capture?.Dispose();
        Capture = null;
        // Creator = null;
        // DialogMatcher = null;
        // ContentMatcher = null;
        // BannerMatcher = null;
        // MarkerMatcher = null;
    }

    private void Process()
    {
        if (Capture == null || Capture.Ptr == IntPtr.Zero) return;
        if (DialogMatcher == null || ContentMatcher == null ||
            BannerMatcher == null || MarkerMatcher == null) return;
        var frameRate = Capture.Get(CapProp.Fps);
        var frame = new Mat();
        if (Creator == null) throw new NullReferenceException();
        var frameCount = Capture.Get(CapProp.FrameCount);
        var markerIndexInDialog = MarkerIndexOfDialog();

        var avgDuration = 0d;
        var frameIndex = 0;
        while (true)
        {
            var tic = Environment.TickCount;
            try
            {
                if (Token.IsCancellationRequested) break;
                if (Capture is not { IsOpened: true }) break;
                if (!Capture.Read(frame)) break;

                frameIndex = (int)Capture.Get(CapProp.PosFrames);
                Callbacks.OnProgress(frameIndex / frameCount);

                if (frameIndex % ((int)frameRate / 5) == 0)
                {
                    var previewFrame = frame.Clone();
                    Task.Run(() => { Callbacks.OnFramePreviewImage(previewFrame); }, Token);
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
                    if (DialogMatcher.Set[dialogIndex].Finished)
                    {
                        Callbacks.OnNewDialog(DialogMatcher.Set[dialogIndex]);
                    }
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
            }
            catch (Exception e)
            {
                Callbacks.OnException(e);
                if (Debugger.IsAttached) throw;
            }
            finally
            {
                var toc = Environment.TickCount;
                Fps(toc - tic);
            }
        }

        Callbacks.OnProgress(1);

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
            var events = new Queue<Event>(
                Creator!.Story.GetTypes(Story.Story.StoryEventType.Dialog | Story.Story.StoryEventType.Marker)
            );
            while (events.TryDequeue(out var ev))
                switch (ev)
                {
                    case Dialog:
                        dialogCount += 1;
                        break;
                    case Marker:
                        markerIndex.Add(dialogCount);
                        break;
                }

            return markerIndex.Select(x => x < 0 ? 0 : x).ToList();
        }


        void Fps(int deltaTime)
        {
            const double alpha = 1d / 100d; // 采样数设置为100

            avgDuration = Math.Abs(frameCount - 1) < double.MinValue
                ? deltaTime
                : avgDuration * (1 - alpha) + deltaTime * alpha;


            var fps = (int)(1d / avgDuration * 1000);
            var etaMs = (frameCount - frameIndex) * avgDuration;
            var eta = new TimeSpan(0, 0, 0, 0, (int)etaMs);
            Callbacks.OnFps(fps, eta);
        }
    }
}
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class VideoProcessorLifecycleTests
{
    [Fact]
    public async Task FactorySessionSurvivesWorkerUntilProcessorDisposal()
    {
        using var fixture = new Fixture();
        using var gate = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new TrackingFactory("qualified-test", false);
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnTaskStarted = () => { started.SetResult(); gate.Wait(TimeSpan.FromSeconds(10)); }
        }, factory);
        Assert.Equal("qualified-test", processor.CaptureState().Metadata!.CaptureIdentity);
        processor.StartProcess();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Throws<InvalidOperationException>(processor.Dispose);
            Assert.Equal(0, factory.Session!.DisposeCount);
        }
        finally { gate.Set(); }
        await processor.StopProcessAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(IntPtr.Zero, factory.Session!.Capture.Ptr);
        Assert.Equal(0, factory.Session.DisposeCount);
        processor.Dispose();
        processor.Dispose();
        Assert.Equal(1, factory.Session.DisposeCount);
    }

    [Theory]
    [InlineData("", false, false)]
    [InlineData("qualified-test", true, true)]
    public void RejectedFactorySessionIsDisposedDuringConstruction(string identity, bool mixed, bool prepareGray)
    {
        using var fixture = new Fixture();
        var factory = new TrackingFactory(identity, mixed);
        Assert.Throws<InvalidOperationException>(() => fixture.Create(new VideoProcessCallbacks(), factory,
            new RecognitionPerformanceOptions { PrepareGrayFrames = prepareGray }));
        Assert.Equal(1, factory.Session!.DisposeCount);
        Assert.Equal(IntPtr.Zero, factory.Session.Capture.Ptr);
    }

    private sealed class TrackingFactory(string identity, bool mixed) : IVideoCaptureFactory
    {
        public TrackingSession? Session { get; private set; }
        public IVideoCaptureSession Open(string videoPath, int previewInterval) =>
            Session = new TrackingSession(new VideoCapture(videoPath), identity, mixed);
    }

    private sealed class TrackingSession(VideoCapture capture, string identity, bool mixed) : IVideoCaptureSession
    {
        public VideoCapture Capture => capture;
        public string Identity => identity;
        public bool HasMixedFrameChannels => mixed;
        public int DisposeCount { get; private set; }
        public void Dispose() { DisposeCount++; capture.Dispose(); }
    }

    [Fact]
    public async Task WaitingForCompletionDoesNotCancelProcessing()
    {
        using var fixture = new Fixture();
        using var gate = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnTaskStarted = () => { started.SetResult(); gate.Wait(TimeSpan.FromSeconds(10)); },
            OnTaskFinished = () => finished = true
        });
        processor.StartProcess();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var completion = processor.WaitForCompletionAsync();
        try { Assert.False(completion.IsCompleted); }
        finally { gate.Set(); }
        await completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(finished);
        Assert.Equal(ProcessStopReason.EndOfStream, processor.StopReason);
        Assert.Equal(12, processor.Performance.FrameCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisabledPreviewSkipsFrameDelivery(bool enabled)
    {
        using var fixture = new Fixture();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previews = 0;
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            IsPreviewEnabled = () => enabled,
            OnFramePreviewImage = _ => Interlocked.Increment(ref previews),
            OnTaskFinished = () => done.TrySetResult()
        });
        processor.StartProcess();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopProcessAsync();
        Assert.Equal(12, processor.Performance.FrameCount);
        if (enabled) Assert.InRange(previews, 1, 2);
        else Assert.Equal(0, previews);
    }

    [Fact]
    public async Task PreviewMetadataMatchesDeliveredImage()
    {
        using var fixture = new Fixture(encodeFrameIndex: true);
        var delivered = new List<(int Index, double Blue)>();
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnFramePreview = (image, index) => delivered.Add((index, CvInvoke.Mean(image).V0))
        });
        processor.StartProcess();
        await processor.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopProcessAsync();
        Assert.NotEmpty(delivered);
        foreach (var frame in delivered)
        {
            Assert.InRange(frame.Index, 0, 11);
            Assert.InRange(frame.Blue, frame.Index * 10 - 5, frame.Index * 10 + 5);
        }
    }

    [Fact]
    public async Task CancellationSavesConsumedFrameInsteadOfPrefetchedPosition()
    {
        using var fixture = new Fixture();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reachedFrame = 0;
        Exception? error = null;
        VideoProcessor? current = null;
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnProgress = _ =>
            {
                if (reachedFrame != 0) return;
                reachedFrame = current!.CaptureState().FrameIndex;
                current.StopProcess();
            },
            OnTaskFinished = () => done.TrySetResult(),
            OnException = e => error = e
        });
        current = processor;
        processor.StartProcess();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopProcessAsync();
        Assert.Null(error);
        Assert.Equal(1, reachedFrame);
        Assert.Equal(1, processor.CaptureState().FrameIndex);
        Assert.Single(processor.CaptureState().Timecodes);
        Assert.Equal(ProcessStopReason.Canceled, processor.StopReason);
    }

    [Fact]
    public async Task StopWaitsForWorkerAndFinishedCallbackBeforeDisposal()
    {
        using var fixture = new Fixture();
        using var gate = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnTaskStarted = () => { started.SetResult(); gate.Wait(TimeSpan.FromSeconds(10)); },
            OnTaskFinished = () => finished = true
        });
        processor.StartProcess();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = processor.StopProcessAsync();
        try { Assert.False(stop.IsCompleted); }
        finally { gate.Set(); }
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(finished);
        Assert.Equal(ProcessStopReason.Canceled, processor.StopReason);
        processor.Dispose();
    }

    [Fact]
    public async Task CallbackFailureStillReleasesDecoder()
    {
        using var fixture = new Fixture();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? reported = null;
        using var processor = fixture.Create(new VideoProcessCallbacks
        {
            OnProgress = _ => throw new InvalidOperationException("Injected callback failure"),
            OnException = e => reported = e,
            OnTaskFinished = () => finished.SetResult()
        });
        var captureProperty = typeof(VideoProcessor).GetProperty("Capture", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var decoder = (VideoCapture)captureProperty.GetValue(processor)!;
        processor.StartProcess();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopProcessAsync();
        Assert.IsType<InvalidOperationException>(reported);
        Assert.Equal(ProcessStopReason.UnexpectedError, processor.StopReason);
        Assert.Equal(IntPtr.Zero, decoder.Ptr);
        Assert.Null(captureProperty.GetValue(processor));
    }

    [Fact]
    public async Task ResumeBackcheckWarmupDoesNotConsumeCheckpointFrameTwice()
    {
        using var fixture = new Fixture();
        VideoProcessor? active = null;
        using var initial = fixture.Create(new VideoProcessCallbacks
        {
            IsPreviewEnabled = () =>
            {
                if (active!.CaptureState().FrameIndex == 2) active.StopProcess();
                return false;
            }
        });
        active = initial;
        initial.StartProcess();
        await initial.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var state = initial.CaptureState();
        Assert.Equal(2, state.FrameIndex);
        // The synthetic clip has no targets. Inject only a pending backcheck to
        // exercise decoder warmup independently of template resource matching.
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(state))!;
        json["Dialog"]!["SearchState"] = JsonSerializer.SerializeToNode(new AdaptiveSearchStateDto(1, 2));
        var checkpoint = json.Deserialize<ProcessingState>()!;
        Exception? error = null;
        using var resumed = fixture.Create(new VideoProcessCallbacks
        {
            IsPreviewEnabled = () => false,
            OnException = e => error = e
        });
        resumed.ApplyState(checkpoint);
        resumed.StartProcess();
        await resumed.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(error);
        Assert.Equal(ProcessStopReason.EndOfStream, resumed.StopReason);
        Assert.Equal(10, resumed.Performance.FrameCount);
        Assert.Equal(12, resumed.CaptureState().FrameIndex);
        Assert.Equal(12, resumed.CaptureState().Timecodes.Count);
        Assert.Equal(state.Timecodes, resumed.CaptureState().Timecodes.Take(2));
    }

    private sealed class Fixture : ITemplateResourceProvider, IProcessingStatePersistence, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "SekaiTools-lifecycle-" + Guid.NewGuid().ToString("N"));
        private string VideoPath => Path.Combine(_directory, "input.avi");
        private string ScriptPath => Path.Combine(_directory, "story.json");
        private string MenuPath => Path.Combine(_directory, "menu.png");

        public Fixture(bool encodeFrameIndex = false)
        {
            Directory.CreateDirectory(_directory);
            using (var frame = new Mat(90, 160, DepthType.Cv8U, 3))
            using (var writer = new VideoWriter(VideoPath, VideoWriter.Fourcc('M', 'J', 'P', 'G'), 30, new Size(160, 90), true))
            {
                Assert.True(writer.IsOpened);
                frame.SetTo(new MCvScalar(20, 40, 60));
                for (var i = 0; i < 12; i++)
                {
                    if (encodeFrameIndex) frame.SetTo(new MCvScalar(i * 10, 40, 60));
                    writer.Write(frame);
                }
            }
            using (var menu = new Mat(32, 32, DepthType.Cv8U, 4))
            {
                menu.SetTo(new MCvScalar(20, 20, 20, 255));
                CvInvoke.Rectangle(menu, new Rectangle(8, 5, 12, 10), new MCvScalar(255, 255, 255, 255), -1);
                CvInvoke.Imwrite(MenuPath, menu);
            }
            File.WriteAllText(ScriptPath, JsonSerializer.Serialize(new SekaiToolsBase.GameScript.GameScript { Snippets = [], TalkData = [], SpecialEffectData = [] }));
        }
        public VideoProcessor Create(VideoProcessCallbacks callbacks, IVideoCaptureFactory? factory = null,
            RecognitionPerformanceOptions? options = null) => new(new Config(VideoPath, ScriptPath, "", performanceOptions: options), callbacks, this, this, factory);
        public string GetVideoProcessResourcePath(string fileName) => MenuPath;
        public void SaveProgress(string key, ProcessingState state) { }
        public void DeleteProgress(string key) { }
        public void AddHistory(ProcessingState state) { }
        public void Dispose()
        {
            File.Delete(VideoPath);
            File.Delete(ScriptPath);
            File.Delete(MenuPath);
            Directory.Delete(_directory);
        }
    }
}

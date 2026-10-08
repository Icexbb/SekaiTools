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

    private sealed class Fixture : ITemplateResourceProvider, IProcessingStatePersistence, IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "SekaiTools-lifecycle-" + Guid.NewGuid().ToString("N"));
        private string VideoPath => Path.Combine(_directory, "input.avi");
        private string ScriptPath => Path.Combine(_directory, "story.json");
        private string MenuPath => Path.Combine(_directory, "menu.png");

        public Fixture()
        {
            Directory.CreateDirectory(_directory);
            using (var frame = new Mat(90, 160, DepthType.Cv8U, 3))
            using (var writer = new VideoWriter(VideoPath, VideoWriter.Fourcc('M', 'J', 'P', 'G'), 30, new Size(160, 90), true))
            {
                Assert.True(writer.IsOpened);
                frame.SetTo(new MCvScalar(20, 40, 60));
                for (var i = 0; i < 12; i++) writer.Write(frame);
            }
            using (var menu = new Mat(32, 32, DepthType.Cv8U, 4))
            {
                menu.SetTo(new MCvScalar(20, 20, 20, 255));
                CvInvoke.Rectangle(menu, new Rectangle(8, 5, 12, 10), new MCvScalar(255, 255, 255, 255), -1);
                CvInvoke.Imwrite(MenuPath, menu);
            }
            File.WriteAllText(ScriptPath, JsonSerializer.Serialize(new SekaiToolsBase.GameScript.GameScript { Snippets = [], TalkData = [], SpecialEffectData = [] }));
        }
        public VideoProcessor Create(VideoProcessCallbacks callbacks) => new(new Config(VideoPath, ScriptPath, ""), callbacks, this, this);
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

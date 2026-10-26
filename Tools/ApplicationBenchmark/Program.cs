using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Emgu.CV;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsInfrastructure.Persistence;
using SekaiToolsInfrastructure.Resources;
using SekaiToolsGUI.Service;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 5) throw new ArgumentException("Usage: ApplicationBenchmark <output> <threads> <buffer> <cancelFraction=0> <videos...>");
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        CvInvoke.NumThreads = int.Parse(args[1]);
        var buffer = int.Parse(args[2]);
        var cancelFraction = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        if (cancelFraction < 0 || cancelFraction >= 1) throw new ArgumentOutOfRangeException(nameof(cancelFraction));
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEKAI_BENCHMARK_RESUME_STATE"))
            && (args.Length != 5 || cancelFraction != 0))
            throw new ArgumentException("External resume state requires one video and no cancellation");
        var cancelFrame = GetCancelFrame();
        if (cancelFrame != null && (args.Length != 5 || cancelFraction != 0 ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEKAI_BENCHMARK_RESUME_STATE"))))
            throw new ArgumentException("Exact frame cancellation requires one video, no fraction and no external resume state");
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var image = new Image { Width = 960, Height = 540 };
        var render = new RenderTargetBitmap(960, 540, 96, 96, PixelFormats.Pbgra32);
        Exception? failure = null;
        var queue = new SequentialTaskQueue<string>(video => RunVideo(video, output, buffer, cancelFraction, dispatcher, image, render));
        queue.TaskFailed += (_, error) => failure ??= error;
        foreach (var video in args.Skip(4)) queue.PendingTasks.Add(Path.GetFullPath(video));
        dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await queue.RunAsync(); }
            catch (Exception error) { failure = error; }
            finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        }));
        Dispatcher.Run();
        if (failure != null) throw new InvalidOperationException("Application benchmark failed", failure);
        if (queue.PendingTasks.Count != 0 || queue.ProcessingTasks.Count != 0 || queue.FinishedTasks.Count != args.Length - 4)
            throw new InvalidOperationException("Queue did not drain correctly");
    }

    private static async Task RunVideo(string video, string output, int buffer, double cancelFraction,
        Dispatcher dispatcher, Image image, RenderTargetBitmap render)
    {
        var directory = Path.Combine(output, Path.GetFileName(video));
        Directory.CreateDirectory(directory);
        var persistence = new ScopedPersistence(Path.Combine(directory, "persistence"));
        var config = new Config(video, Path.ChangeExtension(video, ".json"), Path.ChangeExtension(video, ".txt"),
            new StyleFontConfig(), new ExportStyleConfig(), new TypewriterSetting(),
            performanceOptions: new RecognitionPerformanceOptions { FrameBufferCapacity = buffer });
        long previewCount = 0, previewTicks = 0;
        var resumePath = Environment.GetEnvironmentVariable("SEKAI_BENCHMARK_RESUME_STATE");
        if (!string.IsNullOrEmpty(resumePath) && cancelFraction > 0)
            throw new ArgumentException("External resume state cannot be combined with cancellation");
        ProcessingState? resume = string.IsNullOrEmpty(resumePath) ? null
            : JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(resumePath))
                ?? throw new InvalidDataException("Invalid external resume state");
        var cancelFrame = GetCancelFrame();
        var hasCancellation = cancelFraction > 0 || cancelFrame != null;
        var frameIndexField = cancelFrame == null ? null : typeof(VideoProcessor)
            .GetField("_processedFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("VideoProcessor._processedFrameIndex");
        var started = Stopwatch.GetTimestamp();
        for (var segment = 0; segment < (hasCancellation ? 2 : 1); segment++)
        {
            Exception? failure = null;
            VideoProcessor? active = null;
            using var processor = new VideoProcessor(config, new VideoProcessCallbacks
            {
                // Called on the processing thread after decode, before matching. StopProcess
                // cancels the next loop; this frame still completes and is persisted.
                IsPreviewEnabled = () =>
                {
                    if (segment == 0 && cancelFrame != null && active != null &&
                        (int)frameIndexField!.GetValue(active)! >= cancelFrame.Value) active.StopProcess();
                    return true;
                },
                OnException = error => failure = error,
                OnProgress = progress => { if (segment == 0 && cancelFraction > 0 && progress >= cancelFraction) active?.StopProcess(); },
                OnFramePreviewImage = frame =>
                {
                    var time = Stopwatch.GetTimestamp();
                    dispatcher.Invoke(() =>
                    {
                        image.Source = frame.ToBitmapSource();
                        image.Measure(new Size(960, 540));
                        image.Arrange(new Rect(0, 0, 960, 540));
                        image.UpdateLayout();
                        render.Clear();
                        render.Render(image);
                        if (previewCount == 0)
                        {
                            var pixel = new byte[4];
                            render.CopyPixels(new Int32Rect(480, 270, 1, 1), pixel, 4, 0);
                            if (pixel[3] == 0) throw new InvalidOperationException("WPF preview did not render pixels");
                        }
                    });
                    Interlocked.Increment(ref previewCount);
                    Interlocked.Add(ref previewTicks, Stopwatch.GetTimestamp() - time);
                }
            }, ResourceManager.Instance, persistence);
            active = processor;
            processor.EnableProgressSaving(ProgressStore.GetSaveKey(video, config.ScriptFilePath, config.TranslateFilePath));
            if (resume != null) processor.ApplyState(resume);
            processor.StartProcess();
            await processor.WaitForCompletionAsync();
            if (failure != null) throw new InvalidOperationException("Recognition failed", failure);
            var state = processor.CaptureState();
            if (segment == 0 && hasCancellation)
            {
                if (processor.StopReason != ProcessStopReason.Canceled || state.FrameIndex <= 0)
                    throw new InvalidOperationException("Cancellation was not exercised");
                if (cancelFrame != null && state.FrameIndex != cancelFrame.Value)
                    throw new InvalidOperationException($"Expected exact cancellation at {cancelFrame}, got {state.FrameIndex}");
                var saved = persistence.LoadCheckpoint();
                if (saved.FrameIndex != state.FrameIndex) throw new InvalidOperationException("Final checkpoint was not saved");
                resume = saved;
                File.WriteAllText(Path.Combine(directory, "canceled-state.json"), JsonSerializer.Serialize(state));
                continue;
            }
            if (processor.StopReason is not (ProcessStopReason.Completed or ProcessStopReason.EndOfStream))
                throw new InvalidOperationException($"Unexpected stop: {processor.StopReason}");
            if (previewCount == 0 || persistence.SaveCount == 0)
                throw new InvalidOperationException("Preview or checkpoint writing was not exercised");
            var dialogs = new List<SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet>();
            var banners = new List<SekaiToolsCore.Process.FrameSet.BannerBaseFrameSet>();
            var markers = new List<SekaiToolsCore.Process.FrameSet.MarkerBaseFrameSet>();
            processor.ReplayExportableCallbacks(dialogs.Add, banners.Add, markers.Add);
            var subtitle = processor.GenerateSubtitle(banners, dialogs, markers).ToString();
            var stateJson = JsonSerializer.Serialize(state);
            File.WriteAllText(Path.Combine(directory, "state.json"), stateJson);
            File.WriteAllText(Path.Combine(directory, "subtitle.ass"), subtitle);
            var result = new
            {
                Video = video,
                Threads = CvInvoke.NumThreads,
                Buffer = buffer,
                CancelFraction = cancelFraction,
                CancelFrame = cancelFrame,
                ResumeFrame = resume?.FrameIndex,
                StopReason = processor.StopReason.ToString(),
                ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds,
                PreviewCount = previewCount,
                PreviewMilliseconds = previewTicks * 1000d / Stopwatch.Frequency,
                persistence.SaveCount,
                persistence.SaveMilliseconds,
                PeakMiB = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d,
                StateHash = Hash(stateJson),
                SubtitleHash = Hash(subtitle)
            };
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(result));
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
    }
    private static int? GetCancelFrame()
    {
        var value = Environment.GetEnvironmentVariable("SEKAI_BENCHMARK_CANCEL_FRAME");
        if (string.IsNullOrEmpty(value)) return null;
        if (!int.TryParse(value, out var frame) || frame <= 0)
            throw new ArgumentException("SEKAI_BENCHMARK_CANCEL_FRAME must be a positive integer");
        return frame;
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

internal sealed class ScopedPersistence(string directory) : IProcessingStatePersistence
{
    // Invoke the actual application's atomic JSON writer with an isolated test path.
    private static readonly Action<string, ProcessingState> Save = typeof(ProgressStore)
        .GetMethod("SaveToPath", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Action<string, ProcessingState>>();
    private string? _checkpoint;
    public int SaveCount { get; private set; }
    public double SaveMilliseconds { get; private set; }
    public void SaveProgress(string key, ProcessingState state)
    {
        _checkpoint = Path.Combine(directory, $"{key}.json");
        var start = Stopwatch.GetTimestamp();
        Save(_checkpoint, state);
        SaveCount++;
        SaveMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    public void DeleteProgress(string key)
    {
        var path = Path.Combine(directory, $"{key}.json");
        if (File.Exists(path)) File.Delete(path);
    }
    public void AddHistory(ProcessingState state) => Save(Path.Combine(directory, "history-state.json"), state);
    public ProcessingState LoadCheckpoint() => JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(
        _checkpoint ?? throw new InvalidOperationException("No checkpoint"))) ?? throw new InvalidDataException("Invalid checkpoint");
}

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emgu.CV;
using PeriodicColorCaptureExperiment;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsInfrastructure.Persistence;
using SekaiToolsInfrastructure.Resources;
using SekaiToolsInfrastructure.Video;

if (args.Length is < 5 or > 8)
    throw new ArgumentException("PeriodicColorResumeProbe <video> <qualified-plugin> <new-output-directory> <continuous|cancel|resume> <frame|checkpoint|-> [matchingThreads=default] [buffer=default] [factory=false]");
var video = Path.GetFullPath(args[0]);
var plugin = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var mode = args[3];
if (mode is not ("continuous" or "cancel" or "resume")) throw new ArgumentException("Unknown mode");
var cancelFrame = mode == "cancel" ? int.Parse(args[4]) : (int?)null;
if (cancelFrame is <= 0) throw new ArgumentOutOfRangeException(nameof(cancelFrame));
if (mode == "continuous" && args[4] != "-") throw new ArgumentException("Continuous mode uses '-' instead of a checkpoint");
var threads = args.Length > 5 && args[5] != "default" ? int.Parse(args[5]) : (int?)null;
var buffer = args.Length > 6 && args[6] != "default" ? int.Parse(args[6]) : (int?)null;
var factoryMode = args.Length > 7 && bool.Parse(args[7]);
if (threads is < 1 or > 64 || buffer is < 0 or > 32) throw new ArgumentOutOfRangeException();
if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a fresh output directory");
if (threads.HasValue) CvInvoke.NumThreads = threads.Value;
var config = new Config(video, Path.ChangeExtension(video, ".json"), Path.ChangeExtension(video, ".txt"),
    new StyleFontConfig(), new ExportStyleConfig(), new TypewriterSetting(),
    performanceOptions: new RecognitionPerformanceOptions { FrameBufferCapacity = buffer, EnableFramePrefetch = buffer != 0 });
using var metadataCapture = new VideoCapture(video, VideoCapture.API.Ffmpeg, Tuple.Create((Emgu.CV.CvEnum.CapProp)70, 16));
if (!metadataCapture.IsOpened) throw new InvalidDataException("Cannot read preview interval");
var previewInterval = Math.Max(1, (int)Math.Round(metadataCapture.Get(Emgu.CV.CvEnum.CapProp.Fps) / 5d));
metadataCapture.Dispose();
using var owner = factoryMode ? null : new ExperimentalPeriodicColorCapture(video, plugin, 16, previewInterval);
var factory = factoryMode ? new ResumeCaptureFactory(new PeriodicColorCaptureFactory(plugin)) : null;
var nativePath = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
    .Single(module => string.Equals(module.ModuleName, "cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
var nativeHash = HashFile(nativePath);
var pluginHash = HashFile(plugin);
var previewFrames = 0;
var nonColorPreviewFrames = 0;
var persistence = new ExperimentPersistence(Path.Combine(output, "persistence"), config, nativeHash, pluginHash, previewInterval, factoryMode);
VideoProcessor? active = null;
Exception? failure = null;
var frameField = typeof(VideoProcessor).GetField("_processedFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new MissingFieldException("VideoProcessor._processedFrameIndex");
using var processor = new VideoProcessor(config, new VideoProcessCallbacks
{
    IsPreviewEnabled = () =>
    {
        // This callback runs after decoding and before matching. The requested
        // frame still completes; cancellation is observed at the next loop.
        if (cancelFrame != null && active != null && (int)frameField.GetValue(active)! >= cancelFrame.Value)
            active.StopProcess();
        return true;
    },
    OnFramePreviewImage = image => { Interlocked.Increment(ref previewFrames); if (image.NumberOfChannels != 3) Interlocked.Increment(ref nonColorPreviewFrames); },
    OnException = exception => failure = exception
}, ResourceManager.Instance, persistence, factory);
active = processor;
if (!factoryMode)
{
var captureProperty = typeof(VideoProcessor).GetProperty("Capture", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new MissingMemberException("VideoProcessor.Capture");
(captureProperty.GetValue(processor) as VideoCapture)?.Dispose();
captureProperty.SetValue(processor, owner!.Capture);
}
var metadata = processor.CaptureState().Metadata ?? throw new InvalidDataException("Missing original metadata");
persistence.Initialize(metadata);
ProcessingState? restored = null;
string? sourceCheckpointHash = null;
if (mode == "resume")
{
    var checkpoint = Path.GetFullPath(args[4]);
    sourceCheckpointHash = HashFile(checkpoint);
    var experimentState = JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(checkpoint))
        ?? throw new InvalidDataException("Invalid checkpoint");
    restored = persistence.ValidateAndNormalize(experimentState);
    processor.ApplyState(restored);
}
processor.EnableProgressSaving(ProgressStore.GetSaveKey(video, config.ScriptFilePath, config.TranslateFilePath));
Directory.CreateDirectory(output);
processor.StartProcess();
await processor.WaitForCompletionAsync();
if (failure != null) throw new InvalidOperationException("Periodic color resume qualification failed", failure);
if (previewFrames == 0 || nonColorPreviewFrames != 0) throw new InvalidDataException("Color preview callback qualification failed");
var state = processor.CaptureState();
var tagged = persistence.Tag(state);
var actualCapture = factoryMode ? factory!.Session!.Capture : owner!.Capture;
if (actualCapture.Ptr != IntPtr.Zero) throw new InvalidDataException("Native capture was not released after completion");
var checkpointEquivalent = false;
bool? historyEquivalent = null;
if (mode == "cancel")
{
    if (processor.StopReason != ProcessStopReason.Canceled || state.FrameIndex != cancelFrame)
        throw new InvalidDataException("Cancellation did not stop at the exact requested frame");
    var saved = persistence.LoadCheckpoint();
    persistence.ValidateAndNormalize(saved);
    checkpointEquivalent = JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(tagged);
    if (!checkpointEquivalent) throw new InvalidDataException("Saved checkpoint differs from captured state");
}
else
{
    if (processor.StopReason is not (ProcessStopReason.Completed or ProcessStopReason.EndOfStream))
        throw new InvalidDataException("Unexpected termination");
    if (processor.StopReason == ProcessStopReason.Completed && !persistence.HistoryWritten)
        throw new InvalidDataException("History write was not exercised");
    if (persistence.HistoryWritten)
    {
        historyEquivalent = JsonSerializer.Serialize(persistence.LoadHistory()) == JsonSerializer.Serialize(tagged);
        if (historyEquivalent != true) throw new InvalidDataException("Atomic history differs from final state");
    }
    var dialogs = new List<DialogBaseFrameSet>();
    var banners = new List<BannerBaseFrameSet>();
    var markers = new List<MarkerBaseFrameSet>();
    processor.ReplayExportableCallbacks(dialogs.Add, banners.Add, markers.Add);
    File.WriteAllText(Path.Combine(output, "subtitle.ass"), processor.GenerateSubtitle(banners, dialogs, markers).ToString());
}
if (persistence.SaveCount == 0) throw new InvalidDataException("Atomic progress write was not exercised");
var stateJson = JsonSerializer.Serialize(tagged);
File.WriteAllText(Path.Combine(output, "state.json"), stateJson);
if (factoryMode) processor.Dispose();
var report = new
{
    Video = video, InputHash = HashFile(video), Mode = mode, CancelFrame = cancelFrame,
    ResumeFrame = restored?.FrameIndex, SourceCheckpointHash = sourceCheckpointHash,
    OriginalConfigHash = metadata.ConfigHash, ExperimentConfigHash = tagged.Metadata!.ConfigHash,
    NativePath = nativePath, NativeHash = nativeHash, PluginPath = plugin, PluginHash = pluginHash,
    MatchingThreads = CvInvoke.NumThreads, Buffer = buffer, DecoderThreads = 16,
    FrameIndex = state.FrameIndex, StopReason = processor.StopReason.ToString(),
    persistence.SaveCount, persistence.HistoryWritten, CheckpointEquivalent = checkpointEquivalent,
    HistoryEquivalent = historyEquivalent,
    HistoryPath = persistence.HistoryWritten ? persistence.HistoryPath : null,
    HistoryHash = persistence.HistoryWritten ? HashFile(persistence.HistoryPath) : null,
    CheckpointPath = mode == "cancel" ? persistence.CheckpointPath : null,
    CheckpointHash = mode == "cancel" ? HashFile(persistence.CheckpointPath!) : null,
    StateHash = HashText(stateJson),
    SubtitleHash = mode == "cancel" ? null : HashFile(Path.Combine(output, "subtitle.ass")),
    ProductRejectsExperimentState = !ProcessingStateCompatibility.ValidateAndMigrate(tagged, config,
        factoryMode ? metadata with { CaptureIdentity = null } : metadata).CanRestore,
    ProcessorFactoryCapture = factoryMode, CaptureIdentity = metadata.CaptureIdentity,
    SessionDisposed = factory?.Session?.Disposed,
    ProductFactoryImplementation = factoryMode,
    CaptureDisposed = actualCapture.Ptr == IntPtr.Zero,
    NoPerformanceConclusion = true, PreviewQualified = false, PreviewInterval = previewInterval, PreviewFrames = previewFrames, NonColorPreviewFrames = nonColorPreviewFrames
};
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report));

static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

internal sealed class ExperimentPersistence(string directory, Config config, string nativeHash, string pluginHash, int previewInterval, bool factoryMode) : IProcessingStatePersistence
{
    private static readonly Action<string, ProcessingState> Save = typeof(ProgressStore)
        .GetMethod("SaveToPath", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Action<string, ProcessingState>>();
    private ProcessingStateMetadata? _metadata;
    private string? _experimentHash;
    public int SaveCount { get; private set; }
    public bool HistoryWritten { get; private set; }
    public string? CheckpointPath { get; private set; }
    public string HistoryPath => Path.Combine(directory, "history-state.json");

    public void Initialize(ProcessingStateMetadata metadata)
    {
        _metadata = metadata;
        if (factoryMode)
        {
            if (string.IsNullOrWhiteSpace(metadata.CaptureIdentity)) throw new InvalidDataException("Missing factory identity");
            _experimentHash = metadata.ConfigHash;
            return;
        }
        _experimentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{metadata.ConfigHash}|BenchmarkOnlyPeriodicColorCapture|FFMPEG:None:16:{previewInterval}:{nativeHash}:{pluginHash}")));
    }
    public ProcessingState Tag(ProcessingState original)
    {
        if (_metadata == null || original.Version != ProcessingStateCompatibility.CurrentVersion || original.Metadata != _metadata)
            throw new InvalidDataException("Unexpected original state identity");
        var copy = Clone(original);
        if (factoryMode)
        {
            if (!ProcessingStateCompatibility.ValidateAndMigrate(copy, config, _metadata).CanRestore ||
                ProcessingStateCompatibility.ValidateAndMigrate(copy, config, _metadata with { CaptureIdentity = null }).CanRestore)
                throw new InvalidDataException("Factory checkpoint compatibility differs");
            return copy;
        }
        copy.Metadata = copy.Metadata! with { ConfigHash = _experimentHash! };
        if (ProcessingStateCompatibility.ValidateAndMigrate(copy, config, _metadata).CanRestore)
            throw new InvalidDataException("Product configuration accepted an experiment checkpoint");
        return copy;
    }
    public ProcessingState ValidateAndNormalize(ProcessingState experiment)
    {
        if (factoryMode)
        {
            if (_metadata == null || !ProcessingStateCompatibility.ValidateAndMigrate(experiment, config, _metadata).CanRestore)
                throw new InvalidDataException("Factory checkpoint identity differs");
            return Clone(experiment); // No identity or ConfigHash normalization.
        }
        if (_metadata == null || experiment.Version != ProcessingStateCompatibility.CurrentVersion ||
            experiment.Metadata != (_metadata with { ConfigHash = _experimentHash! }))
            throw new InvalidDataException("Checkpoint input or gray runtime identity differs");
        if (ProcessingStateCompatibility.ValidateAndMigrate(experiment, config, _metadata).CanRestore)
            throw new InvalidDataException("Untagged product checkpoint entered the experiment");
        var copy = Clone(experiment);
        copy.Metadata = _metadata;
        if (!ProcessingStateCompatibility.ValidateAndMigrate(copy, config, _metadata).CanRestore)
            throw new InvalidDataException("Normalized checkpoint incompatible");
        return copy;
    }
    public void SaveProgress(string key, ProcessingState state)
    {
        CheckpointPath = Path.Combine(directory, $"{key}.json");
        Save(CheckpointPath, Tag(state));
        SaveCount++;
    }
    public void DeleteProgress(string key)
    {
        var path = Path.Combine(directory, $"{key}.json");
        if (File.Exists(path)) File.Delete(path);
    }
    public void AddHistory(ProcessingState state)
    {
        Save(HistoryPath, Tag(state));
        HistoryWritten = true;
    }
    public ProcessingState LoadCheckpoint() => JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(
        CheckpointPath ?? throw new InvalidOperationException("No checkpoint"))) ?? throw new InvalidDataException("Invalid checkpoint");
    public ProcessingState LoadHistory() => JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(HistoryPath))
        ?? throw new InvalidDataException("Invalid history");
    private static ProcessingState Clone(ProcessingState state) => JsonSerializer.Deserialize<ProcessingState>(JsonSerializer.Serialize(state))!;
}

internal sealed class ResumeCaptureFactory(IVideoCaptureFactory inner) : IVideoCaptureFactory
{
    public ResumeCaptureSession? Session { get; private set; }
    public IVideoCaptureSession Open(string videoPath, int previewInterval)
    {
        if (Session != null) throw new InvalidOperationException("A probe factory opens only one session");
        return Session = new ResumeCaptureSession(inner.Open(videoPath, previewInterval));
    }
}

internal sealed class ResumeCaptureSession(IVideoCaptureSession inner) : IVideoCaptureSession
{
    public VideoCapture Capture => inner.Capture;
    public string Identity => inner.Identity;
    public bool HasMixedFrameChannels => inner.HasMixedFrameChannels;
    public bool Disposed { get; private set; }
    public void Dispose() { inner.Dispose(); Disposed = true; }
}

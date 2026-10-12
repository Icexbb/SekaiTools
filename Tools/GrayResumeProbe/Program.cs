using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emgu.CV;
using GrayCaptureExperiment;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsInfrastructure.Persistence;
using SekaiToolsInfrastructure.Resources;

if (args.Length is < 5 or > 7)
    throw new ArgumentException("GrayResumeProbe <video> <qualified-plugin> <new-output-directory> <continuous|cancel|resume> <frame|checkpoint|-> [matchingThreads=4] [buffer=8]");
var video = Path.GetFullPath(args[0]);
var plugin = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var mode = args[3];
if (mode is not ("continuous" or "cancel" or "resume")) throw new ArgumentException("Unknown mode");
var cancelFrame = mode == "cancel" ? int.Parse(args[4]) : (int?)null;
if (cancelFrame is <= 0) throw new ArgumentOutOfRangeException(nameof(cancelFrame));
if (mode == "continuous" && args[4] != "-") throw new ArgumentException("Continuous mode uses '-' instead of a checkpoint");
var threads = args.Length > 5 ? int.Parse(args[5]) : 4;
var buffer = args.Length > 6 ? int.Parse(args[6]) : 8;
if (threads is < 1 or > 64 || buffer is < 0 or > 32) throw new ArgumentOutOfRangeException();
if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a fresh output directory");
CvInvoke.NumThreads = threads;
var config = new Config(video, Path.ChangeExtension(video, ".json"), Path.ChangeExtension(video, ".txt"),
    new StyleFontConfig(), new ExportStyleConfig(), new TypewriterSetting(),
    performanceOptions: new RecognitionPerformanceOptions { FrameBufferCapacity = buffer, EnableFramePrefetch = buffer > 0 });
using var owner = new ExperimentalGrayCapture(video, plugin, 16);
var persistence = new ExperimentPersistence(Path.Combine(output, "persistence"), config, owner);
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
        return false;
    },
    OnException = exception => failure = exception
}, ResourceManager.Instance, persistence);
active = processor;
var captureProperty = typeof(VideoProcessor).GetProperty("Capture", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new MissingMemberException("VideoProcessor.Capture");
(captureProperty.GetValue(processor) as VideoCapture)?.Dispose();
captureProperty.SetValue(processor, owner.Capture);
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
if (failure != null) throw new InvalidOperationException("Gray resume qualification failed", failure);
var state = processor.CaptureState();
var tagged = persistence.Tag(state);
if (owner.Capture.Ptr != IntPtr.Zero) throw new InvalidDataException("Native capture was not released after completion");
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
var report = new
{
    Video = video, InputHash = HashFile(video), Mode = mode, CancelFrame = cancelFrame,
    ResumeFrame = restored?.FrameIndex, SourceCheckpointHash = sourceCheckpointHash,
    OriginalConfigHash = metadata.ConfigHash, ExperimentConfigHash = tagged.Metadata!.ConfigHash,
    NativePath = owner.NativePath, NativeHash = owner.NativeHash, PluginPath = owner.PluginPath, PluginHash = owner.PluginHash,
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
    ProductRejectsExperimentState = !ProcessingStateCompatibility.ValidateAndMigrate(tagged, config, metadata).CanRestore,
    CaptureDisposed = owner.Capture.Ptr == IntPtr.Zero,
    NoPerformanceConclusion = true, PreviewQualified = false
};
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report));

static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

internal sealed class ExperimentPersistence(string directory, Config config, ExperimentalGrayCapture owner) : IProcessingStatePersistence
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
        _experimentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{metadata.ConfigHash}|BenchmarkOnlyGrayCapture|FFMPEG:None:16:{owner.NativeHash}:{owner.PluginHash}")));
    }
    public ProcessingState Tag(ProcessingState original)
    {
        if (_metadata == null || original.Version != ProcessingStateCompatibility.CurrentVersion || original.Metadata != _metadata)
            throw new InvalidDataException("Unexpected original state identity");
        var copy = Clone(original);
        copy.Metadata = copy.Metadata! with { ConfigHash = _experimentHash! };
        if (ProcessingStateCompatibility.ValidateAndMigrate(copy, config, _metadata).CanRestore)
            throw new InvalidDataException("Product configuration accepted an experiment checkpoint");
        return copy;
    }
    public ProcessingState ValidateAndNormalize(ProcessingState experiment)
    {
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

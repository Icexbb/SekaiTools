using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using Emgu.CV;
using SekaiToolsInfrastructure.Resources;
using GrayCaptureExperiment;
using PeriodicColorCaptureExperiment;

if (args.Length is < 2 or > 17)
    throw new ArgumentException("Usage: RecognitionBenchmark <video> <output-directory> [runs=3] [preview=false] [mode=Automatic] [prepareGray=false] [threads=default] [profile=true] [warmup=true] [capacity=default] [positionHints=false] [decodeOnly=false] [hardwareDecode=false] [decoderThreads=default] [qualifiedGrayPlugin=none]");
var video = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var runs = args.Length > 2 ? int.Parse(args[2]) : 3;
if (runs < 1) throw new ArgumentOutOfRangeException(nameof(runs), "At least one measured run is required");
var preview = args.Length > 3 && bool.Parse(args[3]);
var mode = args.Length > 4 ? Enum.Parse<RecognitionPerformanceMode>(args[4]) : RecognitionPerformanceMode.Automatic;
var prepareGray = args.Length > 5 && bool.Parse(args[5]);
if (args.Length > 6 && args[6] != "default") CvInvoke.NumThreads = int.Parse(args[6]);
var profile = args.Length <= 7 || bool.Parse(args[7]);
var warmup = args.Length <= 8 || bool.Parse(args[8]);
var capacity = args.Length > 9 && args[9] != "default" ? int.Parse(args[9]) : (int?)null;
var positionHints = args.Length > 10 && bool.Parse(args[10]);
var decodeOnly = args.Length > 11 && bool.Parse(args[11]);
var hardwareDecode = args.Length > 12 && bool.Parse(args[12]);
var decoderThreads = args.Length > 13 ? int.Parse(args[13]) : (int?)null;
var grayPlugin = args.Length > 14 ? Path.GetFullPath(args[14]) : null;
var periodicColor = args.Length > 15 && bool.Parse(args[15]);
var factoryMode = args.Length > 16 && bool.Parse(args[16]);
if (factoryMode && !periodicColor) throw new ArgumentException("Factory qualification requires periodic color");
if (periodicColor && grayPlugin == null) throw new ArgumentException("Periodic color requires a qualified plugin");
if (grayPlugin != null && ((!periodicColor && preview) || prepareGray || hardwareDecode || decodeOnly || decoderThreads != 16))
    throw new ArgumentException("Native gray experiment requires preview/prepareGray/hardwareDecode/decodeOnly=false and decoderThreads=16");
if (decoderThreads is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(decoderThreads));
if (hardwareDecode && decoderThreads != null) throw new ArgumentException("Do not combine hardware and thread experiments");
if (decodeOnly && decoderThreads != null) throw new ArgumentException("Use DecoderProbe for decoder-only thread experiments");
for (var run = warmup ? 0 : 1; run <= runs; run++)
{
    if (decodeOnly)
    {
        using var capture = hardwareDecode
            ? new VideoCapture(video, VideoCapture.API.Ffmpeg,
                [Tuple.Create(Emgu.CV.CvEnum.CapProp.HwAcceleration, (int)Emgu.CV.CvEnum.VideoAccelerationType.Any)])
            : new VideoCapture(video);
        if (!capture.IsOpened) throw new InvalidOperationException("Cannot open video");
        using var reader = new BufferedVideoReader(capture, capacity ?? 0, CancellationToken.None, prepareGray);
        var start = Stopwatch.GetTimestamp();
        var frames = 0;
        var decode = TimeSpan.Zero;
        var preprocess = TimeSpan.Zero;
        while (true)
        {
            using var frame = reader.Read();
            decode += frame.DecodeDuration;
            preprocess += frame.PreprocessDuration;
            if (!frame.Succeeded) break;
            frames++;
        }
        var elapsed = Stopwatch.GetElapsedTime(start);
        var decoded = new { Run = run, Warmup = run == 0, Video = video, DecodeOnly = true,
            Backend = capture.BackendName, HardwareDecode = hardwareDecode,
            Acceleration = ((Emgu.CV.CvEnum.VideoAccelerationType)(int)capture.Get(Emgu.CV.CvEnum.CapProp.HwAcceleration)).ToString(),
            Frames = frames, ElapsedMilliseconds = elapsed.TotalMilliseconds,
            Width = (int)capture.Get(Emgu.CV.CvEnum.CapProp.FrameWidth), Height = (int)capture.Get(Emgu.CV.CvEnum.CapProp.FrameHeight),
            Fps = capture.Get(Emgu.CV.CvEnum.CapProp.Fps), DeclaredFrames = capture.Get(Emgu.CV.CvEnum.CapProp.FrameCount),
            DecodeMilliseconds = decode.TotalMilliseconds, PreprocessMilliseconds = preprocess.TotalMilliseconds,
            Capacity = reader.Capacity, OpenCvThreads = CvInvoke.NumThreads,
            PeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 };
        File.WriteAllText(Path.Combine(output, $"run-{run}.json"), JsonSerializer.Serialize(decoded));
        Console.WriteLine(JsonSerializer.Serialize(decoded));
        continue;
    }
    Exception? failure = null;
    var dialogs = new List<DialogBaseFrameSet>();
    var banners = new List<BannerBaseFrameSet>();
    var markers = new List<MarkerBaseFrameSet>();
    var options = new RecognitionPerformanceOptions { Mode = mode, PrepareGrayFrames = prepareGray,
        EnableFramePrefetch = capacity != 0 };
    // Keep the same runner source buildable against older baseline revisions.
    typeof(RecognitionPerformanceOptions).GetProperty("CollectDetailedMetrics")?.SetValue(options, profile);
    typeof(RecognitionPerformanceOptions).GetProperty("FrameBufferCapacity")?.SetValue(options, capacity);
    typeof(RecognitionPerformanceOptions).GetProperty("UseContentPositionHints")?.SetValue(options, positionHints);
    var config = new Config(video, Path.ChangeExtension(video, ".json"),
        Path.ChangeExtension(video, ".txt"), new StyleFontConfig(), new ExportStyleConfig(), new TypewriterSetting(),
        performanceOptions: options);
    // Declare the plugin owner first so the processor always disposes before it.
    var previewInterval = 0;
    if (periodicColor && !factoryMode)
    {
        using var metadataCapture = new VideoCapture(video, VideoCapture.API.Ffmpeg, Tuple.Create((Emgu.CV.CvEnum.CapProp)70, 16));
        if (!metadataCapture.IsOpened) throw new InvalidOperationException("Cannot read preview interval");
        previewInterval = Math.Max(1, (int)Math.Round(metadataCapture.Get(Emgu.CV.CvEnum.CapProp.Fps) / 5d));
    }
    using var grayCapture = grayPlugin == null || periodicColor ? null : new ExperimentalGrayCapture(video, grayPlugin, decoderThreads!.Value);
    using var periodicCapture = periodicColor && !factoryMode ? new ExperimentalPeriodicColorCapture(video, grayPlugin!, 16, previewInterval) : null;
    var factory = factoryMode ? new QualificationCaptureFactory(grayPlugin!, video, previewInterval) : null;
    var previewFrames = 0;
    var nonColorPreviewFrames = 0;
    using var processor = new VideoProcessor(config, new VideoProcessCallbacks
        { IsPreviewEnabled = () => preview, OnException = e => failure = e,
            OnFramePreviewImage = image => { Interlocked.Increment(ref previewFrames); if (image.NumberOfChannels != 3) Interlocked.Increment(ref nonColorPreviewFrames); },
            OnNewDialog = dialogs.Add, OnNewBanner = banners.Add, OnNewMarker = markers.Add },
        ResourceManager.Instance, new NoPersistence(), factory);
    if (factoryMode) previewInterval = factory!.PreviewInterval;
    // Experimental decoder replacement stays in this isolated benchmark, never in product options.
    var captureProperty = typeof(VideoProcessor).GetProperty("Capture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Benchmark capture hook unavailable");
    if (!factoryMode && (hardwareDecode || decoderThreads != null))
    {
        var replacement = periodicCapture?.Capture ?? grayCapture?.Capture ?? new VideoCapture(video, VideoCapture.API.Ffmpeg,
            decoderThreads is { } requestedThreads
                ? [Tuple.Create((Emgu.CV.CvEnum.CapProp)70, requestedThreads)]
                : [Tuple.Create(Emgu.CV.CvEnum.CapProp.HwAcceleration, (int)Emgu.CV.CvEnum.VideoAccelerationType.Any)]);
        if (!replacement.IsOpened) { replacement.Dispose(); throw new InvalidOperationException("Hardware capture cannot open video"); }
        try
        {
            (captureProperty.GetValue(processor) as VideoCapture)?.Dispose();
            captureProperty.SetValue(processor, replacement);
        }
        catch { replacement.Dispose(); throw; }
    }
    var activeCapture = (VideoCapture?)captureProperty.GetValue(processor) ?? throw new InvalidOperationException("Missing capture");
    var actualDecoderThreads = (int)activeCapture.Get((Emgu.CV.CvEnum.CapProp)70);
    if (decoderThreads != null && actualDecoderThreads != decoderThreads)
        throw new InvalidOperationException("Decoder thread request was not applied");
    var decodeBackend = activeCapture.BackendName;
    var decodeAcceleration = ((Emgu.CV.CvEnum.VideoAccelerationType)(int)activeCapture.Get(Emgu.CV.CvEnum.CapProp.HwAcceleration)).ToString();
    // Fingerprint loaded runtimes before starting the measured processing interval.
    var nativeLibraries = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
        .Where(module => module.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase) ||
            module.ModuleName.Equals("opencv_videoio_ffmpeg500_64.dll", StringComparison.OrdinalIgnoreCase))
        .Select(module =>
        {
            using var stream = File.OpenRead(module.FileName);
            return new { Path = module.FileName, Hash = Convert.ToHexString(SHA256.HashData(stream)) };
        }).ToArray();
    Console.WriteLine($"Run {run}: {(run == 0 ? "warmup" : "measured")}");
    var started = Stopwatch.GetTimestamp();
    processor.StartProcess();
    await processor.WaitForCompletionAsync();
    if (preview && (previewFrames == 0 || nonColorPreviewFrames != 0)) throw new InvalidDataException("Color preview callback qualification failed");
    if (failure != null) throw new InvalidOperationException("Recognition failed", failure);
    if (processor.StopReason is not (ProcessStopReason.Completed or ProcessStopReason.EndOfStream))
        throw new InvalidOperationException($"Unexpected stop: {processor.StopReason}");
    var state = processor.CaptureState();
    var originalConfigHash = state.Metadata?.ConfigHash;
    var stateHashBeforeExperimentTag = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state))));
    if (factoryMode)
    {
        var metadata = state.Metadata ?? throw new InvalidDataException("Missing factory metadata");
        if (metadata.CaptureIdentity != factory!.Session.Identity ||
            !ProcessingStateCompatibility.ValidateAndMigrate(state, config, metadata).CanRestore ||
            ProcessingStateCompatibility.ValidateAndMigrate(state, config, metadata with { CaptureIdentity = null }).CanRestore)
            throw new InvalidDataException("Factory checkpoint identity qualification failed");
    }
    else if (hardwareDecode || decoderThreads != null)
    {
        var metadata = state.Metadata ?? throw new InvalidOperationException("Missing state metadata");
        var experimentIdentity = periodicCapture != null
            ? $"{metadata.ConfigHash}|BenchmarkOnlyPeriodicColorCapture|{decodeBackend}:{decodeAcceleration}:{decoderThreads}:{previewInterval}:{periodicCapture.NativeHash}:{periodicCapture.PluginHash}"
            : grayCapture == null
            ? $"{metadata.ConfigHash}|BenchmarkOnlyDecoder|{decodeBackend}:{decodeAcceleration}:{decoderThreads}"
            : $"{metadata.ConfigHash}|BenchmarkOnlyGrayCapture|{decodeBackend}:{decodeAcceleration}:{decoderThreads}:{grayCapture.NativeHash}:{grayCapture.PluginHash}";
        state.Metadata = metadata with { ConfigHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(experimentIdentity))) };
        if (ProcessingStateCompatibility.ValidateAndMigrate(state, config, metadata).CanRestore)
            throw new InvalidOperationException("Decoder experiment checkpoint must be rejected by product configuration");
    }
    var stateJson = JsonSerializer.Serialize(state);
    File.WriteAllText(Path.Combine(output, $"state-{run}.json"), stateJson);
    var subtitle = processor.GenerateSubtitle(banners, dialogs, markers).ToString();
    File.WriteAllText(Path.Combine(output, $"subtitle-{run}.ass"), subtitle);
    var result = new
    {
        Run = run, Warmup = run == 0, Video = video, Preview = preview, Mode = mode.ToString(), PrepareGray = prepareGray,
        OpenCvThreads = CvInvoke.NumThreads,
        DetailedProfile = profile,
        Resolution = processor.VideoInfo.Resolution, VideoFrames = processor.VideoInfo.FrameCount,
        FrameBufferCapacity = capacity,
        ContentPositionHints = positionHints,
        HardwareDecode = hardwareDecode,
        NativeGrayCapture = grayCapture != null,
        NativePeriodicColorCapture = periodicColor,
        ProcessorFactoryCapture = factoryMode,
        CaptureIdentity = state.Metadata?.CaptureIdentity,
        PeriodicColorNativeHash = factoryMode
            ? nativeLibraries.Single(x => Path.GetFileName(x.Path).Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).Hash
            : periodicCapture?.NativeHash,
        PeriodicColorPluginHash = factoryMode
            ? nativeLibraries.Single(x => Path.GetFileName(x.Path).Equals("opencv_videoio_ffmpeg500_64.dll", StringComparison.OrdinalIgnoreCase)).Hash
            : periodicCapture?.PluginHash,
        PeriodicColorInterval = previewInterval,
        PreviewFrames = previewFrames, NonColorPreviewFrames = nonColorPreviewFrames,
        GrayCaptureNativeHash = grayCapture?.NativeHash,
        GrayCapturePluginHash = grayCapture?.PluginHash,
        NativeLibraries = nativeLibraries,
        RequestedDecoderThreads = decoderThreads, ActualDecoderThreads = actualDecoderThreads,
        OriginalConfigHash = originalConfigHash, StateHashBeforeExperimentTag = stateHashBeforeExperimentTag,
        DecodeBackend = decodeBackend,
        DecodeAcceleration = decodeAcceleration,
        WallMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
        PeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64,
        StopReason = processor.StopReason.ToString(), Performance = processor.Performance,
        // Reflection keeps this runner usable against the pre-instrumentation baseline.
        Matching = typeof(VideoProcessor).GetProperty("MatchingPerformance")?.GetValue(processor),
        StateHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stateJson))),
        RecognitionHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            { state.FrameIndex, state.ContentFinished, state.StopReason, state.Timecodes, state.Dialog, state.Banner, state.Marker })))),
        SubtitleHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(subtitle)))
    };
    if (factoryMode)
    {
        processor.Dispose();
        if (!factory!.Session.Disposed || factory.Session.Capture.Ptr != 0)
            throw new InvalidDataException("Processor did not release its capture session");
        File.WriteAllText(Path.Combine(output, $"ownership-{run}.json"), JsonSerializer.Serialize(new
            { CaptureDisposed = true, SessionDisposed = true, ProcessorFactoryCapture = true, factory.Session.Identity }));
    }
    File.WriteAllText(Path.Combine(output, $"run-{run}.json"), JsonSerializer.Serialize(result,
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(result));
}

sealed class NoPersistence : IProcessingStatePersistence
{
    public void SaveProgress(string key, ProcessingState state) { }
    public void DeleteProgress(string key) { }
    public void AddHistory(ProcessingState state) { }
}

sealed class QualificationCaptureFactory(string plugin, string video, int interval) : IVideoCaptureFactory
{
    public QualificationCaptureSession Session { get; private set; } = null!;
    public int PreviewInterval { get; private set; }
    public IVideoCaptureSession Open(string videoPath, int previewInterval)
    {
        if (videoPath != video || (interval != 0 && previewInterval != interval)) throw new InvalidDataException("Factory request differs");
        PreviewInterval = previewInterval;
        Session = new QualificationCaptureSession(new SekaiToolsInfrastructure.Video.PeriodicColorCaptureFactory(plugin).Open(videoPath, previewInterval));
        return Session;
    }
}

sealed class QualificationCaptureSession(IVideoCaptureSession inner) : IVideoCaptureSession
{
    public VideoCapture Capture => inner.Capture;
    public string Identity => inner.Identity;
    public bool HasMixedFrameChannels => inner.HasMixedFrameChannels;
    public bool Disposed { get; private set; }
    public void Dispose() { inner.Dispose(); Disposed = true; }
}

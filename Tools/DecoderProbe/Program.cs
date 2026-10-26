using DecoderProbe;
using System.Diagnostics;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;

if (args.Length is < 2 or > 5) throw new ArgumentException("Usage: DecoderProbe <input-directory-or-video> <output-json> [candidate=Msmf|FfmpegAny|FfmpegD3D11|FfmpegThreadsN] [frameLimit=64;0=all] [extraByteAuditFrames=comma-separated]");
var selection = args.Length >= 3 ? args[2] : "Msmf";
var frameLimit = args.Length >= 4 ? int.Parse(args[3]) : 64;
var extraByteAuditFrames = args.Length == 5 ? args[4].Split(',').Select(int.Parse).ToHashSet() : new HashSet<int>();
if (extraByteAuditFrames.Any(frame => frame <= 0)) throw new ArgumentException("Byte audit frame indices must be positive");
if (frameLimit < 0) throw new ArgumentOutOfRangeException(nameof(frameLimit));
int? requestedThreads = null;
if (selection.StartsWith("FfmpegThreads", StringComparison.Ordinal))
{
    if (!int.TryParse(selection["FfmpegThreads".Length..], out var threads) || threads < 1 || threads > 64)
        throw new ArgumentException("FFmpeg thread count must be 1..64");
    requestedThreads = threads;
}
// OpenCV 5 videoio.hpp: CAP_PROP_N_THREADS=70, open-only, FFmpeg.
// Emgu 5.0.0 does not name this property; keep the numeric cast in this probe.
const CapProp decoderThreadsProperty = (CapProp)70;
var requestedAcceleration = selection switch
{
    "Msmf" => (VideoAccelerationType?)null,
    "FfmpegAny" => VideoAccelerationType.Any,
    "FfmpegD3D11" => VideoAccelerationType.D3D11,
    _ when requestedThreads != null => (VideoAccelerationType?)null,
    _ => throw new ArgumentException("Unknown candidate")
};
CvInvoke.NumThreads = 4;
var reports = new List<object>();
IEnumerable<string> videos = File.Exists(args[0]) ? new[] { Path.GetFullPath(args[0]) }
    : Directory.EnumerateFiles(args[0], "*", SearchOption.AllDirectories)
        .Where(path => new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm" }.Contains(Path.GetExtension(path).ToLowerInvariant())).Order();
foreach (var video in videos)
{
    try
    {
        using var reference = new VideoCapture(video, VideoCapture.API.Ffmpeg);
        using var candidate = requestedThreads is { } threadCount
            ? new VideoCapture(video, VideoCapture.API.Ffmpeg, [Tuple.Create(decoderThreadsProperty, threadCount)])
            : requestedAcceleration is { } requested
            ? new VideoCapture(video, VideoCapture.API.Ffmpeg, [Tuple.Create(CapProp.HwAcceleration, (int)requested)])
            : new VideoCapture(video, VideoCapture.API.Msmf);
        if (!reference.IsOpened) throw new InvalidOperationException("Reference cannot open video");
        if (!candidate.IsOpened)
        {
            reports.Add(new { Video = video, Candidate = selection, Status = "Candidate unavailable" });
            continue;
        }
        using var left = new Mat();
        using var right = new Mat();
        var frames = 0;
        var maxPixelDifference = 0d;
        var sequentialPixelDifferenceFrames = 0;
        int? firstSequentialDifferenceFrame = null;
        var sequentialDifferenceSamples = new List<int>();
        var byteAudits = new List<object>();
        var byteMismatchFrames = 0;
        var normByteDisagreements = 0;
        var positivePixelAudits = 0;
        void AuditBytes(int frameIndex, bool seek, double norm)
        {
            var difference = BytePixelComparison.Compare(left, right);
            if (difference.Maximum != 0) byteMismatchFrames++;
            if (difference.Maximum != norm) normByteDisagreements++;
            byteAudits.Add(new { FrameIndex = frameIndex, Seek = seek, Norm = norm, difference.Maximum, difference.UnequalBytes });
        }
        var maxTimestampDifference = 0d;
        var sameFrameIndices = true;
        var start = Stopwatch.GetTimestamp();
        while (frameLimit == 0 || frames < frameLimit)
        {
            var a = reference.Read(left);
            var b = candidate.Read(right);
            if (a != b) throw new InvalidDataException("Different read success");
            if (!a) break;
            if (left.Size != right.Size || left.NumberOfChannels != right.NumberOfChannels)
                throw new InvalidDataException("Different decoded image shape");
            var pixelDifference = CvInvoke.Norm(left, right, NormType.C);
            maxPixelDifference = Math.Max(maxPixelDifference, pixelDifference);
            if ((pixelDifference > 0 && positivePixelAudits < 16) || frames == 0 || (frames + 1) % 1024 == 0 || extraByteAuditFrames.Contains(frames + 1))
            {
                AuditBytes(frames + 1, false, pixelDifference);
                if (pixelDifference > 0) positivePixelAudits++;
            }
            if (pixelDifference > 0)
            {
                sequentialPixelDifferenceFrames++;
                firstSequentialDifferenceFrame ??= frames + 1;
                if (sequentialDifferenceSamples.Count < 16) sequentialDifferenceSamples.Add(frames + 1);
            }
            maxTimestampDifference = Math.Max(maxTimestampDifference, Math.Abs(reference.Get(CapProp.PosMsec) - candidate.Get(CapProp.PosMsec)));
            sameFrameIndices &= reference.Get(CapProp.PosFrames) == candidate.Get(CapProp.PosFrames);
            frames++;
        }
        var seekChecks = new List<object>();
        if (requestedThreads != null)
        {
            var declaredFrames = (int)reference.Get(CapProp.FrameCount);
            foreach (var fraction in new[] { 0d, .25d, .5d, .75d, .95d })
            {
                var target = (int)Math.Floor(Math.Max(0, declaredFrames - 1) * fraction);
                if (!reference.Set(CapProp.PosFrames, target) || !candidate.Set(CapProp.PosFrames, target))
                    throw new InvalidDataException($"Cannot seek to frame {target}");
                var checkedFrames = 0;
                for (var offset = 0; offset < 3 && target + offset < declaredFrames; offset++)
                {
                    if (!reference.Read(left) || !candidate.Read(right))
                        throw new InvalidDataException($"Cannot decode seek frame {target + offset}");
                    if (left.Size != right.Size || left.NumberOfChannels != right.NumberOfChannels)
                        throw new InvalidDataException("Different seek image shape");
                    var seekDifference = CvInvoke.Norm(left, right, NormType.C);
                    maxPixelDifference = Math.Max(maxPixelDifference, seekDifference);
                    AuditBytes(target + offset + 1, true, seekDifference);
                    maxTimestampDifference = Math.Max(maxTimestampDifference,
                        Math.Abs(reference.Get(CapProp.PosMsec) - candidate.Get(CapProp.PosMsec)));
                    sameFrameIndices &= reference.Get(CapProp.PosFrames) == target + offset + 1 &&
                        candidate.Get(CapProp.PosFrames) == target + offset + 1;
                    checkedFrames++;
                }
                seekChecks.Add(new { Target = target, Frames = checkedFrames });
            }
        }
        var actualThreads = (int)candidate.Get(decoderThreadsProperty);
        var threadRequestApplied = requestedThreads == null || actualThreads == requestedThreads;
        var actualAcceleration = (VideoAccelerationType)(int)candidate.Get(CapProp.HwAcceleration);
        reports.Add(new
        {
            Video = video,
            Candidate = selection,
            RequestedByteAuditFrames = extraByteAuditFrames.Order().ToArray(),
            ByteAudits = byteAudits,
            ByteMismatchFrames = byteMismatchFrames,
            NormByteDisagreements = normByteDisagreements,
            LogicalProcessors = Environment.ProcessorCount,
            SequentialPixelDifferenceFrames = sequentialPixelDifferenceFrames,
            FirstSequentialDifferenceFrame = firstSequentialDifferenceFrame,
            SequentialDifferenceSamples = sequentialDifferenceSamples,
            FrameLimit = frameLimit,
            SeekChecks = seekChecks,
            RequestedThreads = requestedThreads,
            ReferenceThreads = (int)reference.Get(decoderThreadsProperty),
            ActualThreads = actualThreads,
            ThreadRequestApplied = threadRequestApplied,
            RequestedAcceleration = requestedAcceleration?.ToString(),
            ActualAcceleration = actualAcceleration.ToString(),
            ReferenceAcceleration = ((VideoAccelerationType)(int)reference.Get(CapProp.HwAcceleration)).ToString(),
            Status = normByteDisagreements != 0 ? "Inconclusive: norm and byte checks disagree" : !threadRequestApplied ? "Rejected: thread request not applied" : frames > 0 && maxPixelDifference == 0 && byteMismatchFrames == 0 && maxTimestampDifference < 0.01 && sameFrameIndices
                ? (frameLimit == 0 ? "Full sequential decode equivalent; recognition still required" : "Prefix equivalent; full verification still required") : "Rejected: decoded output differs",
            Frames = frames,
            MaxPixelDifference = maxPixelDifference,
            MaxTimestampDifference = maxTimestampDifference,
            SameFrameIndices = sameFrameIndices,
            ProbeSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds,
            ReferenceBackend = reference.BackendName,
            CandidateBackend = candidate.BackendName
        });
    }
    catch (Exception error) { reports.Add(new { Video = video, Candidate = selection, Status = "Probe failed", Error = error.Message }); }
}
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(reports));

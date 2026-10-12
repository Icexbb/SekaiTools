using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using DecoderProbe;
using Emgu.CV;
using Emgu.CV.CvEnum;
using PluginRetrieveProbe;

if (args.Length is < 4 or > 6)
    throw new ArgumentException("Usage: PluginRetrieveProbe <video> <qualified-plugin-dll> <new-output-json> <verify|copy|gray|discard> [frameLimit=64;0=EOF] [decoderThreads=16]");
var video = Path.GetFullPath(args[0]);
var plugin = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var mode = args[3];
var limit = args.Length > 4 ? int.Parse(args[4]) : 64;
var threads = args.Length > 5 ? int.Parse(args[5]) : 16;
if (mode is not ("verify" or "copy" or "gray" or "discard") || limit < 0 || threads is < 1 or > 64)
    throw new ArgumentException("Invalid probe parameters");
if (File.Exists(output)) throw new IOException("Use a new output file");
CvInvoke.NumThreads = 4;
using var capture = new PluginCapture(plugin, video, mode, threads);
using var reference = mode == "verify" ? new VideoCapture(video, VideoCapture.API.Ffmpeg) : null;
if (reference != null && (!reference.IsOpened || reference.BackendName != "FFMPEG" || (int)reference.Get((CapProp)70) != threads))
    throw new InvalidOperationException("Reference backend or threads differ");
using var expected = new Mat();
using var expectedGray = new Mat();
var declared = (int)capture.Get((int)CapProp.FrameCount);
var frames = 0;
var eof = false;
var grabTicks = 0L;
var retrieveTicks = 0L;
var differences = new List<object>();
using var colorFingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using var grayFingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
var started = Stopwatch.GetTimestamp();
while (limit == 0 || frames < limit)
{
    var read = reference?.Read(expected);
    var call = Stopwatch.GetTimestamp();
    var grabbed = capture.Grab();
    grabTicks += Stopwatch.GetTimestamp() - call;
    if (read != null && read != grabbed) throw new InvalidDataException("Read success differs");
    if (!grabbed) { eof = true; break; }
    call = Stopwatch.GetTimestamp();
    capture.Retrieve();
    retrieveTicks += Stopwatch.GetTimestamp() - call;
    frames++;
    var index = (int)capture.Get((int)CapProp.PosFrames);
    if (index != frames) throw new InvalidDataException("Plugin frame index differs");
    if (reference != null)
    {
        if ((int)reference.Get(CapProp.PosFrames) != index) throw new InvalidDataException("Reference index differs");
        var timestamp = Math.Abs(reference.Get(CapProp.PosMsec) - capture.Get((int)CapProp.PosMsec));
        var color = BytePixelComparison.Compare(expected, capture.Color);
        CvInvoke.CvtColor(expected, expectedGray, ColorConversion.Bgr2Gray);
        var gray = BytePixelComparison.Compare(expectedGray, capture.Gray);
        colorFingerprint.AppendData((byte[])capture.Color.GetData(false));
        grayFingerprint.AppendData((byte[])capture.Gray.GetData(false));
        if (timestamp != 0 || color.Maximum != 0 || gray.Maximum != 0)
            differences.Add(new { Frame = frames, TimestampDifference = timestamp,
                ColorMaximum = color.Maximum, ColorUnequalBytes = color.UnequalBytes,
                GrayMaximum = gray.Maximum, GrayUnequalBytes = gray.UnequalBytes });
    }
}
var elapsed = Stopwatch.GetElapsedTime(started);
if (frames != (limit == 0 ? declared : Math.Min(limit, declared)) || (limit == 0 && !eof))
    throw new InvalidDataException("Expected prefix or EOF frame count not reached");
if (differences.Count != 0) Environment.ExitCode = 1;
var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
    .Where(m => m.ModuleName.Contains("cvextern", StringComparison.OrdinalIgnoreCase) || m.ModuleName.Contains("videoio_ffmpeg", StringComparison.OrdinalIgnoreCase))
    .Select(m => new { Path = m.FileName, Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(m.FileName))) }).ToArray();
var report = new { Video = video, InputHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(video))),
    Plugin = plugin, PluginHash = PluginCapture.SupportedPluginHash, capture.PluginVersion, capture.ProvidedApi,
    Mode = mode, Frames = frames, DeclaredFrames = declared, ReachedEof = eof, FrameLimit = limit,
    DecoderThreads = (int)capture.Get(70), OpenCvThreads = CvInvoke.NumThreads,
    capture.Width, capture.Height, capture.CallbackType,
    ElapsedSeconds = elapsed.TotalSeconds, GrabSeconds = (double)grabTicks / Stopwatch.Frequency,
    RetrieveSeconds = (double)retrieveTicks / Stopwatch.Frequency,
    CallbackSeconds = (double)capture.CallbackTicks / Stopwatch.Frequency,
    VerificationPassed = mode == "verify" ? differences.Count == 0 : (bool?)null,
    ColorFingerprint = mode == "verify" ? Convert.ToHexString(colorFingerprint.GetHashAndReset()) : null,
    GrayFingerprint = mode == "verify" ? Convert.ToHexString(grayFingerprint.GetHashAndReset()) : null,
    Differences = differences, NoPerformanceConclusion = mode == "verify", NativeLibraries = native };
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output, json);
Console.WriteLine(json);

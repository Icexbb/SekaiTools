using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DecoderProbe;
using Emgu.CV;
using Emgu.CV.CvEnum;

if (args.Length is < 3 or > 6)
    throw new ArgumentException("Usage: DecodeStageProbe <video> <new-output-json> <verify|split|read> [frameLimit=64;0=EOF] [decoderThreads=16] [format=Bgr|Bgra]");
var video = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var mode = args[2];
if (mode is not ("verify" or "split" or "read")) throw new ArgumentException("Unknown mode");
var limit = args.Length > 3 ? int.Parse(args[3]) : 64;
var threads = args.Length > 4 ? int.Parse(args[4]) : 16;
var format = args.Length > 5 ? args[5] : "Bgr";
if (format is not ("Bgr" or "Bgra")) throw new ArgumentException("Unknown output format");
if (limit < 0 || threads is < 1 or > 64) throw new ArgumentOutOfRangeException();
if (File.Exists(output)) throw new IOException("Use a new output file; do not overwrite evidence");
CvInvoke.NumThreads = 4;
var parameters = new List<Tuple<CapProp, int>> { Tuple.Create((CapProp)70, threads) };
if (format == "Bgra") parameters.Add(Tuple.Create(CapProp.Format, CvInvoke.MakeType(DepthType.Cv8U, 4)));
using var capture = new VideoCapture(video, VideoCapture.API.Ffmpeg, parameters.ToArray());
if (!capture.IsOpened || capture.BackendName != "FFMPEG" || (int)capture.Get((CapProp)70) != threads)
    throw new InvalidOperationException("Requested software FFmpeg decoder unavailable");
if ((int)capture.Get(CapProp.HwAcceleration) != (int)VideoAccelerationType.None || capture.FlipType != null)
    throw new InvalidOperationException("Unexpected conversion settings");
if (format == "Bgra" && (int)capture.Get(CapProp.Format) != CvInvoke.MakeType(DepthType.Cv8U, 4))
    throw new InvalidOperationException("BGRA format request was not applied");
var declaredFrames = (int)capture.Get(CapProp.FrameCount);
using var image = new Mat();
var frames = 0;
var lastChannels = 0;
var eof = false;
object result;
if (mode == "verify")
{
    using var reference = new VideoCapture(video, VideoCapture.API.Ffmpeg);
    if (!reference.IsOpened || reference.BackendName != "FFMPEG") throw new InvalidOperationException("Reference unavailable");
    using var expected = new Mat();
    using var converted = new Mat();
    using var expectedGray = new Mat();
    using var candidateGray = new Mat();
    var differences = new List<object>();
    while (limit == 0 || frames < limit)
    {
        var read = reference.Read(expected);
        var grab = capture.Grab();
        if (read != grab) throw new InvalidDataException("Read and Grab success differ");
        if (!grab) { eof = true; break; }
        if (!capture.Retrieve(image) || image.IsEmpty) throw new InvalidDataException("Retrieve failed after Grab");
        frames++;
        lastChannels = image.NumberOfChannels;
        if (lastChannels != (format == "Bgra" ? 4 : 3)) throw new InvalidDataException("Unexpected output channels");
        var index = (int)capture.Get(CapProp.PosFrames);
        if (index != frames || index != (int)reference.Get(CapProp.PosFrames))
            throw new InvalidDataException("Frame indices differ");
        var delta = Math.Abs(capture.Get(CapProp.PosMsec) - reference.Get(CapProp.PosMsec));
        var comparable = image;
        var grayMaximum = 0;
        var grayUnequalBytes = 0;
        var nonOpaquePixels = 0;
        if (format == "Bgra")
        {
            CvInvoke.CvtColor(image, converted, ColorConversion.Bgra2Bgr);
            comparable = converted;
            CvInvoke.CvtColor(expected, expectedGray, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(image, candidateGray, ColorConversion.Bgra2Gray);
            var gray = BytePixelComparison.Compare(expectedGray, candidateGray);
            grayMaximum = gray.Maximum;
            grayUnequalBytes = gray.UnequalBytes;
            var pixels = (byte[])image.GetData(false);
            for (var offset = 3; offset < pixels.Length; offset += 4)
                if (pixels[offset] != 255) nonOpaquePixels++;
        }
        var bytes = BytePixelComparison.Compare(expected, comparable);
        if (delta != 0 || bytes.Maximum != 0 || grayMaximum != 0 || nonOpaquePixels != 0)
            differences.Add(new
            {
                Frame = frames,
                TimestampDifference = delta,
                bytes.Maximum,
                bytes.UnequalBytes,
                GrayMaximum = grayMaximum,
                GrayUnequalBytes = grayUnequalBytes,
                NonOpaquePixels = nonOpaquePixels
            });
    }
    result = new
    {
        ReferenceDecoderThreads = (int)reference.Get((CapProp)70),
        VerificationPassed = differences.Count == 0,
        Differences = differences,
        NoPerformanceConclusion = true
    };
    if (differences.Count != 0) Environment.ExitCode = 1;
}
else
{
    var grabTicks = 0L;
    var retrieveTicks = 0L;
    var readTicks = 0L;
    var started = Stopwatch.GetTimestamp();
    while (limit == 0 || frames < limit)
    {
        var call = Stopwatch.GetTimestamp();
        bool succeeded;
        if (mode == "split")
        {
            succeeded = capture.Grab();
            grabTicks += Stopwatch.GetTimestamp() - call;
            if (succeeded)
            {
                call = Stopwatch.GetTimestamp();
                if (!capture.Retrieve(image) || image.IsEmpty) throw new InvalidDataException("Retrieve failed after Grab");
                retrieveTicks += Stopwatch.GetTimestamp() - call;
            }
        }
        else
        {
            succeeded = capture.Read(image);
            readTicks += Stopwatch.GetTimestamp() - call;
        }
        if (!succeeded) { eof = true; break; }
        frames++;
        lastChannels = image.NumberOfChannels;
        if (lastChannels != (format == "Bgra" ? 4 : 3)) throw new InvalidDataException("Unexpected output channels");
        if ((int)capture.Get(CapProp.PosFrames) != frames) throw new InvalidDataException("Unexpected frame index");
    }
    var elapsed = Stopwatch.GetElapsedTime(started);
    result = new
    {
        ElapsedSeconds = elapsed.TotalSeconds,
        GrabSeconds = (double)grabTicks / Stopwatch.Frequency,
        RetrieveSeconds = (double)retrieveTicks / Stopwatch.Frequency,
        ReadSeconds = (double)readTicks / Stopwatch.Frequency,
        NoRecognitionOrPrefetch = true
    };
}
if (limit == 0 && (!eof || frames != declaredFrames))
    throw new InvalidDataException("Full decode did not reach the declared frame count");
if (limit > 0 && frames != Math.Min(limit, declaredFrames))
    throw new InvalidDataException("Prefix did not reach the expected frame count");
var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
    .Where(module => module.ModuleName.Contains("cvextern", StringComparison.OrdinalIgnoreCase)
                  || module.ModuleName.Contains("videoio_ffmpeg", StringComparison.OrdinalIgnoreCase))
    .Select(module => new
    {
        Path = module.FileName,
        Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName)))
    }).ToArray();
if (!native.Any(module => Path.GetFileName(module.Path).Contains("cvextern", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Cannot identify the loaded native runtime");
var report = new
{
    Video = video,
    InputSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(video))),
    Mode = mode,
    OutputFormat = format,
    ActualFormat = (int)capture.Get(CapProp.Format),
    FrameLimit = limit,
    Frames = frames,
    DeclaredFrames = declaredFrames,
    ReachedEof = eof,
    Backend = capture.BackendName,
    DecoderThreads = (int)capture.Get((CapProp)70),
    OpenCvThreads = CvInvoke.NumThreads,
    Width = (int)capture.Get(CapProp.FrameWidth),
    Height = (int)capture.Get(CapProp.FrameHeight),
    Channels = lastChannels,
    NativeLibraries = native,
    Result = result
};
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output, json);
Console.WriteLine(json);

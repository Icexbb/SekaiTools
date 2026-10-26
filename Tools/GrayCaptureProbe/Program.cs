using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using DecoderProbe;
using Emgu.CV;
using Emgu.CV.CvEnum;

if (args.Length != 3) throw new ArgumentException("GrayCaptureProbe <video> <qualified-plugin> <new-report>");
var video = Path.GetFullPath(args[0]);
var plugin = Path.GetFullPath(args[1]);
var report = Path.GetFullPath(args[2]);
if (File.Exists(report)) throw new IOException("Do not overwrite evidence");
if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException();
const string qualifiedHash = "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31";
static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
if (Hash(plugin) != qualifiedHash) throw new InvalidDataException("Unqualified plugin");
CvInvoke.NumThreads = 4;
var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
var nativeHash = Hash(native);
var library = NativeLibrary.Load(plugin);
var core = NativeLibrary.Load(native);
VideoCapture? candidate = null;
var differences = new List<object>();
var verified = 0;
var disposed = false;
try
{
    var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(library, "opencv_videoio_capture_plugin_init_v1"));
    var api = initialize(1, 1, 0);
    if (api == 0) throw new InvalidDataException("Plugin initialization failed");
    var create = Marshal.GetDelegateForFunctionPointer<Create>(NativeLibrary.GetExport(core, "sekaiCreateGrayCapture"));
    var error = new byte[2048];
    var pointer = create(api, video, 16, error, error.Length);
    if (pointer == 0) throw new InvalidOperationException(System.Text.Encoding.UTF8.GetString(error).TrimEnd('\0'));
    var constructor = typeof(VideoCapture).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
        null, [typeof(IntPtr), typeof(bool)], null) ?? throw new MissingMethodException("Capture pointer constructor unavailable");
    candidate = (VideoCapture)constructor.Invoke([pointer, true]);
    if (!candidate.IsOpened || candidate.BackendName != "FFMPEG" || candidate.Get((CapProp)70) != 16 ||
        candidate.Get(CapProp.HwAcceleration) != 0 || candidate.Get(CapProp.Format) != CvInvoke.MakeType(DepthType.Cv8U, 1))
        throw new InvalidDataException("Unexpected candidate configuration");
    using var reference = new VideoCapture(video, VideoCapture.API.Ffmpeg, Tuple.Create((CapProp)70, 16));
    using var color = new Mat();
    using var expected = new Mat();
    using var actual = new Mat();
    void VerifyFrame()
    {
        if (!reference.Read(color) || !candidate.Read(actual)) throw new InvalidDataException("Read failed");
        if (actual.NumberOfChannels != 1) throw new InvalidDataException("Expected grayscale output");
        CvInvoke.CvtColor(color, expected, ColorConversion.Bgr2Gray);
        var comparison = BytePixelComparison.Compare(expected, actual);
        var index = candidate.Get(CapProp.PosFrames);
        var referenceIndex = reference.Get(CapProp.PosFrames);
        var delta = Math.Abs(candidate.Get(CapProp.PosMsec) - reference.Get(CapProp.PosMsec));
        if (comparison.Maximum != 0 || index != referenceIndex || delta != 0)
            differences.Add(new { index, referenceIndex, delta, comparison.Maximum, comparison.UnequalBytes });
        verified++;
    }
    for (var i = 0; i < 64; i++) VerifyFrame();
    var count = (int)candidate.Get(CapProp.FrameCount);
    foreach (var position in new[] { 0, count / 4, count / 2, count * 3 / 4, count - 3 })
    {
        if (!candidate.Set(CapProp.PosFrames, position) || !reference.Set(CapProp.PosFrames, position))
            throw new InvalidDataException("Seek rejected");
        for (var i = 0; i < 3; i++) VerifyFrame();
    }
    candidate.Dispose();
    disposed = candidate.Ptr == IntPtr.Zero;
    if (!disposed) throw new InvalidDataException("Capture pointer not released");
    File.WriteAllText(report, JsonSerializer.Serialize(new
    {
        Video = video,
        InputHash = Hash(video),
        Plugin = plugin,
        PluginHash = qualifiedHash,
        Native = native,
        NativeHash = nativeHash,
        FramesVerified = verified,
        SeekPoints = 5,
        Disposed = disposed,
        Differences = differences,
        VerificationPassed = differences.Count == 0,
        NoPerformanceConclusion = true
    }, new JsonSerializerOptions { WriteIndented = true }));
    if (differences.Count != 0) Environment.ExitCode = 1;
}
finally
{
    // Release the capture before unloading its callback table and code.
    candidate?.Dispose();
    NativeLibrary.Free(core);
    NativeLibrary.Free(library);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate nint Initialize(int abi, int api, nint reserved);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate nint Create(nint api, [MarshalAs(UnmanagedType.LPUTF8Str)] string video, int threads, [Out] byte[] error, int capacity);

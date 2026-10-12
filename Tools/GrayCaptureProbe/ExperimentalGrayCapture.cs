using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace GrayCaptureExperiment;

// Research only. The plugin stays loaded until the native capture is disposed.
internal sealed class ExperimentalGrayCapture : IDisposable
{
    internal const string SupportedPluginHash = "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31";
    private nint _pluginLibrary, _coreLibrary;
    public VideoCapture Capture { get; private set; } = null!;
    public string PluginPath { get; }
    public string PluginHash { get; }
    public string NativePath { get; }
    public string NativeHash { get; }

    public ExperimentalGrayCapture(string video, string plugin, int threads)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException();
        PluginPath = Path.GetFullPath(plugin);
        PluginHash = Hash(PluginPath);
        if (PluginHash != SupportedPluginHash) throw new InvalidDataException("Unqualified plugin");
        var constructor = typeof(VideoCapture).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IntPtr), typeof(bool)], null) ?? throw new MissingMethodException("Capture pointer constructor unavailable");
        _ = CvInvoke.NumThreads; // Initialize Emgu before locating the actual runtime.
        NativePath = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
        NativeHash = Hash(NativePath);
        nint pointer = 0;
        Release? release = null;
        try
        {
            _pluginLibrary = NativeLibrary.Load(PluginPath);
            _coreLibrary = NativeLibrary.Load(NativePath);
            release = Marshal.GetDelegateForFunctionPointer<Release>(NativeLibrary.GetExport(_coreLibrary, "cveVideoCaptureRelease2"));
            var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(_pluginLibrary, "opencv_videoio_capture_plugin_init_v1"));
            var api = initialize(1, 1, 0);
            if (api == 0) throw new InvalidDataException("Plugin initialization failed");
            var create = Marshal.GetDelegateForFunctionPointer<Create>(NativeLibrary.GetExport(_coreLibrary, "sekaiCreateGrayCapture"));
            var error = new byte[2048];
            pointer = create(api, Path.GetFullPath(video), threads, error, error.Length);
            if (pointer == 0) throw new InvalidOperationException(System.Text.Encoding.UTF8.GetString(error).TrimEnd('\0'));
            Capture = (VideoCapture)constructor.Invoke([pointer, true]);
            pointer = 0; // Ownership transferred to the Emgu wrapper.
            if (!Capture.IsOpened || Capture.BackendName != "FFMPEG" || Capture.Get((CapProp)70) != threads ||
                Capture.Get(CapProp.HwAcceleration) != 0 || Capture.Get(CapProp.Format) != CvInvoke.MakeType(DepthType.Cv8U, 1))
                throw new InvalidDataException("Unexpected gray capture configuration");
        }
        catch
        {
            if (pointer != 0) release!(ref pointer);
            Dispose();
            throw;
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        Capture?.Dispose();
        if (_coreLibrary != 0) { NativeLibrary.Free(_coreLibrary); _coreLibrary = 0; }
        if (_pluginLibrary != 0) { NativeLibrary.Free(_pluginLibrary); _pluginLibrary = 0; }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Initialize(int abi, int api, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Create(nint api, [MarshalAs(UnmanagedType.LPUTF8Str)] string video, int threads, [Out] byte[] error, int capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Release(ref nint capture);
}

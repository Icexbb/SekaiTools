using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace PeriodicColorCaptureExperiment;

// Research only. The plugin stays loaded until the native capture is disposed.
internal sealed class ExperimentalPeriodicColorCapture : IDisposable
{
    internal const string SupportedPluginHash = "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31";
    private nint _pluginLibrary, _coreLibrary;
    public VideoCapture Capture { get; private set; } = null!;
    public string PluginPath { get; }
    public string PluginHash { get; }
    public string NativePath { get; }
    public string NativeHash { get; }

    public ExperimentalPeriodicColorCapture(string video, string plugin, int threads, int previewInterval, bool bindPublicCapture = false)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException();
        PluginPath = Path.GetFullPath(plugin);
        PluginHash = Hash(PluginPath);
        if (PluginHash != SupportedPluginHash) throw new InvalidDataException("Unqualified plugin");
        var constructor = bindPublicCapture ? null : typeof(VideoCapture).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
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
            var error = new byte[2048];
            if (bindPublicCapture)
            {
                Capture = new VideoCapture(Path.GetFullPath(video));
                var bind = Marshal.GetDelegateForFunctionPointer<Bind>(NativeLibrary.GetExport(_coreLibrary, "sekaiBindPeriodicColorCapture"));
                if (bind(Capture.Ptr, api, Path.GetFullPath(video), threads, previewInterval, error, error.Length) != 1)
                    throw new InvalidOperationException(System.Text.Encoding.UTF8.GetString(error).TrimEnd('\0'));
            }
            else
            {
                var create = Marshal.GetDelegateForFunctionPointer<Create>(NativeLibrary.GetExport(_coreLibrary, "sekaiCreatePeriodicColorCapture"));
                pointer = create(api, Path.GetFullPath(video), threads, previewInterval, error, error.Length);
                if (pointer == 0) throw new InvalidOperationException(System.Text.Encoding.UTF8.GetString(error).TrimEnd('\0'));
                Capture = (VideoCapture)constructor!.Invoke([pointer, true]);
                pointer = 0; // Ownership transferred to the Emgu wrapper.
            }
            if (!Capture.IsOpened || Capture.BackendName != "FFMPEG" || Capture.Get((CapProp)70) != threads ||
                Capture.Get(CapProp.HwAcceleration) != 0 || Capture.Get(CapProp.Format) != CvInvoke.MakeType(DepthType.Cv8U, 3))
                throw new InvalidDataException("Unexpected periodic color capture configuration");
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

    internal void VerifyFailedBindingPreservesCapture(string video, int threads, int previewInterval)
    {
        var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(_pluginLibrary, "opencv_videoio_capture_plugin_init_v1"));
        var bind = Marshal.GetDelegateForFunctionPointer<Bind>(NativeLibrary.GetExport(_coreLibrary, "sekaiBindPeriodicColorCapture"));
        var pointer = Capture.Ptr;
        var position = Capture.Get(CapProp.PosFrames);
        var frameCount = Capture.Get(CapProp.FrameCount);
        var missing = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(video))!, $"{Guid.NewGuid():N}.missing.mp4");
        if (File.Exists(missing)) throw new IOException("Negative fixture unexpectedly exists");
        foreach (var fixture in new[] { (Path.GetFullPath(video), 0), (missing, previewInterval) })
        {
            var error = new byte[2048];
            if (bind(pointer, initialize(1, 1, 0), fixture.Item1, threads, fixture.Item2, error, error.Length) != 0 || error[0] == 0)
                throw new InvalidDataException("Invalid binding was not rejected with an error");
            if (Capture.Ptr != pointer || !Capture.IsOpened || Capture.Get(CapProp.PosFrames) != position ||
                Capture.Get(CapProp.FrameCount) != frameCount)
                throw new InvalidDataException("Failed binding changed the existing capture");
        }
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
    private delegate nint Create(nint api, [MarshalAs(UnmanagedType.LPUTF8Str)] string video, int threads, int previewInterval, [Out] byte[] error, int capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Bind(nint capture, nint api, [MarshalAs(UnmanagedType.LPUTF8Str)] string video, int threads, int previewInterval, [Out] byte[] error, int capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Release(ref nint capture);
}


using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace PluginRetrieveProbe;

// Research only: pinned capture ABI v1/API v1, synchronous callback, Windows x64.
// Pixel pointers are valid only inside the callback; never retain a borrowed Mat.
internal sealed class PluginCapture : IDisposable
{
    internal const string SupportedPluginHash = "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31";
    [StructLayout(LayoutKind.Sequential)]
    private struct Header
    {
        public nuint Size;
        public uint MinimumApi, Api, Major, Minor, Patch;
        public nint Status, Description;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Api
    {
        public Header Header;
        public int Backend;
        public nint Open, Release, Get, Set, Grab, Retrieve, OpenWithParams;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Initialize(int abi, int api, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string video, int camera,
        [In] int[] parameters, uint count, out nint capture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Release(nint capture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetProperty(nint capture, int property, out double value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GrabFrame(nint capture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FrameCallback(int stream, nint data, int step, int width, int height, int type, nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RetrieveFrame(nint capture, int stream, FrameCallback callback, nint context);

    private nint _library, _capture;
    private readonly Release _release;
    private readonly GetProperty _get;
    private readonly GrabFrame _grab;
    private readonly RetrieveFrame _retrieve;
    private readonly FrameCallback _callback;
    private readonly string _mode;
    private Exception? _callbackError;
    private bool _callbackSeen;
    public Mat Color { get; } = new();
    public Mat Gray { get; } = new();
    public long CallbackTicks { get; private set; }
    public int CallbackType { get; private set; }
    public string PluginVersion { get; }
    public uint ProvidedApi { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public PluginCapture(string plugin, string video, string mode, int threads)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8 || Marshal.SizeOf<Header>() != 48 || Marshal.SizeOf<Api>() != 112)
            throw new PlatformNotSupportedException("Only the audited Windows x64 ABI layout is supported");
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(plugin))) != SupportedPluginHash)
            throw new InvalidOperationException("Unqualified FFmpeg plugin; do not call its ABI");
        _mode = mode;
        _library = NativeLibrary.Load(plugin);
        try
        {
            var init = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(_library, "opencv_videoio_capture_plugin_init_v1"));
            var pointer = init(1, 1, 0);
            if (pointer == 0) throw new InvalidOperationException("Capture ABI v1/API v1 unavailable");
            var header = Marshal.PtrToStructure<Header>(pointer);
            if (header.Size < 112 || header.MinimumApi > 1 || header.Api < 1 || header.Major is not (4 or 5))
                throw new InvalidOperationException("Unexpected capture API header");
            var api = Marshal.PtrToStructure<Api>(pointer);
            if (api.Backend != (int)VideoCapture.API.Ffmpeg || new[] { api.Release, api.Get, api.Grab, api.Retrieve, api.OpenWithParams }.Any(p => p == 0))
                throw new InvalidOperationException("Incomplete FFmpeg capture API");
            PluginVersion = $"{header.Major}.{header.Minor}.{header.Patch}";
            ProvidedApi = header.Api;
            _release = Marshal.GetDelegateForFunctionPointer<Release>(api.Release);
            _get = Marshal.GetDelegateForFunctionPointer<GetProperty>(api.Get);
            _grab = Marshal.GetDelegateForFunctionPointer<GrabFrame>(api.Grab);
            _retrieve = Marshal.GetDelegateForFunctionPointer<RetrieveFrame>(api.Retrieve);
            _callback = OnFrame;
            var open = Marshal.GetDelegateForFunctionPointer<Open>(api.OpenWithParams);
            if (open(video, 0, [70, threads], 1, out _capture) != 0 || _capture == 0)
                throw new InvalidOperationException("Plugin capture open failed");
            if ((int)Get(70) != threads || (int)Get((int)CapProp.HwAcceleration) != (int)VideoAccelerationType.None)
                throw new InvalidOperationException("Requested software threads not applied");
        }
        catch
        {
            if (_capture != 0 && _release != null) _release(_capture);
            NativeLibrary.Free(_library);
            _library = _capture = 0;
            Color.Dispose(); Gray.Dispose();
            throw;
        }
    }

    public double Get(int property)
    {
        if (_get(_capture, property, out var value) != 0 || !double.IsFinite(value))
            throw new InvalidOperationException($"Plugin property unavailable: {property}");
        return value;
    }
    public bool Grab() => _grab(_capture) == 0;
    public void Retrieve()
    {
        _callbackSeen = false;
        var result = _retrieve(_capture, 0, _callback, 0);
        GC.KeepAlive(_callback);
        if (_callbackError != null) throw new InvalidOperationException("Frame callback failed", _callbackError);
        if (result != 0 || !_callbackSeen) throw new InvalidDataException("Plugin retrieval failed or omitted callback");
    }
    private int OnFrame(int stream, nint data, int step, int width, int height, int type, nint context)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            if (_callbackSeen || stream != 0 || data == 0 || width <= 0 || height <= 0 || width > 16384 || height > 16384 || step < checked(width * 3))
                throw new InvalidDataException("Invalid synchronous pixel callback");
            // The pinned plugin may use legacy 3-bit Mat depth or current 5-bit depth.
            if (type is not (16 or 64)) throw new InvalidDataException($"Expected 8UC3 callback, got {type}");
            _callbackSeen = true;
            CallbackType = type; Width = width; Height = height;
            if (_mode != "discard")
            {
                using var borrowed = new Mat(height, width, DepthType.Cv8U, 3, data, step);
                if (_mode is "copy" or "verify") borrowed.CopyTo(Color);
                if (_mode is "gray" or "verify") CvInvoke.CvtColor(borrowed, Gray, ColorConversion.Bgr2Gray);
            }
            return 0;
        }
        catch (Exception error) { _callbackError = error; return -1; }
        finally { CallbackTicks += Stopwatch.GetTimestamp() - start; }
    }
    public void Dispose()
    {
        if (_library == 0) return;
        var result = _capture == 0 ? 0 : _release(_capture);
        _capture = 0;
        Color.Dispose(); Gray.Dispose();
        NativeLibrary.Free(_library); _library = 0;
        if (result != 0) throw new InvalidOperationException("Plugin capture release failed");
    }
}

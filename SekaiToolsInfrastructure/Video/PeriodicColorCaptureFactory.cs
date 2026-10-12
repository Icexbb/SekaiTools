using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Abstractions;

namespace SekaiToolsInfrastructure.Video;

/// <summary>
/// Explicitly injected capture for the qualified Windows x64 runtime.
/// Does not replace or select a native runtime, or change product defaults.
/// </summary>
public sealed class PeriodicColorCaptureFactory(string pluginPath) : IVideoCaptureFactory
{
    public const string QualifiedNativeHash = "5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984";
    public const string QualifiedExpandedNativeHash = "EC795EC4496E2DAB48A593A3E3FC94DEDC35FFB05404BF6A3D9DA7CCBFDB35D1";
    public const string QualifiedPluginHash = "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31";

    public static PeriodicColorCaptureFactory ForCurrentRuntime()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException();
        var native = GetQualifiedRuntimePath();
        return new PeriodicColorCaptureFactory(Path.Combine(Path.GetDirectoryName(native)!, "opencv_videoio_ffmpeg500_64.dll"));
    }

    public IVideoCaptureSession Open(string videoPath, int previewInterval)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException();
        if (previewInterval is < 1 or > 1000000) throw new ArgumentOutOfRangeException(nameof(previewInterval));
        var plugin = Path.GetFullPath(pluginPath);
        if (Hash(plugin) != QualifiedPluginHash) throw new InvalidDataException("未验证的捕获插件");
        var native = GetQualifiedRuntimePath();
        var nativeHash = Hash(native);
        if (nativeHash != QualifiedNativeHash && nativeHash != QualifiedExpandedNativeHash)
            throw new InvalidDataException("未验证的周期彩色运行库");
        return new Session(Path.GetFullPath(videoPath), plugin, native, nativeHash, previewInterval);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string GetQualifiedRuntimePath()
    {
        _ = CvInvoke.NumThreads;
        using var process = Process.GetCurrentProcess();
        var paths = process.Modules.Cast<ProcessModule>()
            .Where(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return SelectQualifiedRuntimePath(paths, Hash);
    }

    internal static string SelectQualifiedRuntimePath(IEnumerable<string> paths, Func<string, string> hash)
    {
        var modules = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (modules.Length == 0) throw new InvalidDataException("未找到已加载的 OpenCV 运行库");
        var fingerprints = modules.Select(hash).ToArray();
        if (fingerprints.All(value => value != QualifiedNativeHash && value != QualifiedExpandedNativeHash))
            throw new InvalidDataException("当前 OpenCV 运行库不支持周期彩色实验。请关闭该实验设置，或使用包含周期彩色扩展的候选包。");
        // Two DLL instances have independent native state, even with equal bytes.
        // Never bind a capture allocated by Emgu to an arbitrarily chosen module.
        if (modules.Length != 1)
            throw new InvalidDataException("加载了多份 OpenCV 运行库，无法安全启用周期彩色实验。请使用干净的候选包目录并重启程序。运行库路径：" + string.Join("；", modules));
        return modules[0];
    }

    private sealed class Session : IVideoCaptureSession
    {
        private nint _plugin, _native;
        public VideoCapture Capture { get; private set; } = null!;
        public string Identity { get; }
        public bool HasMixedFrameChannels => true;

        public Session(string video, string plugin, string native, string nativeHash, int interval)
        {
            Identity = $"PeriodicColor:FFMPEG:None:16:{interval}:{nativeHash}:{QualifiedPluginHash}";
            try
            {
                _plugin = NativeLibrary.Load(plugin);
                _native = NativeLibrary.Load(native);
                var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(
                    NativeLibrary.GetExport(_plugin, "opencv_videoio_capture_plugin_init_v1"));
                var api = initialize(1, 1, 0);
                if (api == 0) throw new InvalidDataException("捕获插件初始化失败");
                var bind = Marshal.GetDelegateForFunctionPointer<Bind>(
                    NativeLibrary.GetExport(_native, "sekaiBindPeriodicColorCapture"));
                Capture = new VideoCapture(video);
                var error = new byte[2048];
                if (bind(Capture.Ptr, api, video, 16, interval, error, error.Length) != 1)
                    throw new InvalidOperationException(Encoding.UTF8.GetString(error).TrimEnd('\0'));
                if (!Capture.IsOpened || Capture.BackendName != "FFMPEG" || Capture.Get((CapProp)70) != 16 ||
                    Capture.Get(CapProp.HwAcceleration) != 0 || Capture.Get(CapProp.Format) != CvInvoke.MakeType(DepthType.Cv8U, 3))
                    throw new InvalidDataException("捕获器配置与已验证模式不一致");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            Capture?.Dispose();
            // Emgu can retain a capture when its grab thread cannot stop. In
            // that case retain the plugin too, rather than unload live code.
            if (Capture != null && Capture.Ptr != 0)
                throw new InvalidOperationException("捕获器尚未释放，保留原生模块");
            if (_native != 0) { NativeLibrary.Free(_native); _native = 0; }
            if (_plugin != 0) { NativeLibrary.Free(_plugin); _plugin = 0; }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Initialize(int abi, int api, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Bind(nint capture, nint api, [MarshalAs(UnmanagedType.LPUTF8Str)] string video,
        int threads, int interval, [Out] byte[] error, int capacity);
}

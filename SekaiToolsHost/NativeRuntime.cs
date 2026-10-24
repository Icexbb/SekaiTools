using System.Runtime.InteropServices;
using Emgu.CV;

namespace SekaiToolsHost;

public static class NativeRuntime
{
    public static (bool Available, string Reason) Check()
    {
        if (!OperatingSystem.IsMacOS()) return (true, "开发主机；不代表 macOS 已验收");
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
            return (false, "macOS 首版仅支持 Apple Silicon 原生 ARM64 进程");
        var custom = Environment.GetEnvironmentVariable("SEKAITOOLS_CVEXTERN_PATH");
        if (!string.IsNullOrWhiteSpace(custom))
            return Path.IsPathFullyQualified(custom) && File.Exists(custom)
                ? (true, "使用开发者提供的原生库，仍需验证 macOS 15/26 兼容性")
                : (false, "SEKAITOOLS_CVEXTERN_PATH 必须指向存在的绝对路径");
        return OperatingSystem.IsMacOSVersionAtLeast(27)
            ? (true, "官方 mini 原生包；视频读取行为尚待验证")
            : (false, "官方 Emgu 5 mini 原生库要求 macOS 27；15/26 需要兼容运行库。下载和翻译仍可使用。");
    }

    public static void Configure()
    {
        var check = Check();
        if (!check.Available) throw new PlatformNotSupportedException(check.Reason);
        if (!OperatingSystem.IsMacOS()) return;
        var custom = Environment.GetEnvironmentVariable("SEKAITOOLS_CVEXTERN_PATH");
        if (string.IsNullOrWhiteSpace(custom)) return;
        NativeLibrary.SetDllImportResolver(typeof(CvInvoke).Assembly, (name, _, _) =>
            name.Contains("cvextern", StringComparison.OrdinalIgnoreCase) ? NativeLibrary.Load(custom) : IntPtr.Zero);
    }
}

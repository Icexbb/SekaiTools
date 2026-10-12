using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.Intrinsics.X86;
using System.Diagnostics;
using Emgu.CV;

if (args.Length != 1) throw new ArgumentException("Usage: OpenCvCapabilities <output-directory>");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
// Capability discovery only: do not change global optimization or device settings.
var build = CvInvoke.BuildInformation;
File.WriteAllText(Path.Combine(output, "build.txt"), build);
object? deviceReport = null;
string? deviceError = null;
var haveOpenCl = CvInvoke.HaveOpenCL;
var useOpenCl = CvInvoke.UseOpenCL;
try
{
    if (haveOpenCl)
    {
        // The default device is owned by OpenCV and must not be disposed.
        var device = Emgu.CV.Ocl.Device.Default;
        deviceReport = new { device.Name, device.VendorName, device.Version,
            device.DriverVersion, device.Available, device.CompilerAvailable };
    }
}
catch (Exception error) { deviceError = error.ToString(); }
object? loadedNativeLibraries = null;
string? loadedNativeError = null;
try
{
    loadedNativeLibraries = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
        .Where(module => module.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)
            || module.ModuleName.StartsWith("libcvextern", StringComparison.Ordinal))
        .Select(module => new { Path = module.FileName,
            Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName))) }).ToArray();
}
catch (Exception error) { loadedNativeError = error.ToString(); }
object? nativeExperimentInfo = null;
string? nativeExperimentError = null;
try
{
    var module = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
        .FirstOrDefault(item => item.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase));
    if (module != null)
    {
        var handle = NativeLibrary.Load(module.FileName);
        try
        {
            if (NativeLibrary.TryGetExport(handle, "sekaiExperimentInfo", out var address))
            {
                var query = Marshal.GetDelegateForFunctionPointer<ExperimentInfoQuery>(address);
                var required = query(IntPtr.Zero, 0);
                if (required < 2 || required > 16384) throw new InvalidOperationException("Invalid native diagnostic size");
                var buffer = Marshal.AllocHGlobal(required);
                try
                {
                    if (query(buffer, required) != required) throw new InvalidOperationException("Native diagnostic size changed");
                    var raw = Marshal.PtrToStringUTF8(buffer, required - 1) ?? throw new InvalidOperationException("Missing native diagnostic");
                    nativeExperimentInfo = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split('=', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally { NativeLibrary.Free(handle); }
    }
}
catch (Exception error) { nativeExperimentError = error.ToString(); }
var report = new
{
    TimestampUtc = DateTimeOffset.UtcNow,
    RuntimeInformation.OSDescription,
    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Environment.ProcessorCount,
    // These are managed-process feature checks, not evidence of native dispatch.
    ManagedCpuFeatures = new
    {
        Sse2 = Sse2.IsSupported, Sse3 = Sse3.IsSupported, Ssse3 = Ssse3.IsSupported,
        Sse41 = Sse41.IsSupported, Sse42 = Sse42.IsSupported,
        Avx = Avx.IsSupported, Avx2 = Avx2.IsSupported, Fma = Fma.IsSupported,
        Avx512F = Avx512F.IsSupported
    },
    CvInvoke.UseOptimized, CvInvoke.NumThreads,
    HaveOpenCl = haveOpenCl, UseOpenCl = useOpenCl,
    Device = deviceReport, DeviceError = deviceError,
    LoadedNativeLibraries = loadedNativeLibraries, LoadedNativeError = loadedNativeError,
    NativeExperimentInfo = nativeExperimentInfo, NativeExperimentError = nativeExperimentError,
    IppEnvironment = Environment.GetEnvironmentVariable("OPENCV_IPP"),
    ManagedVersion = typeof(CvInvoke).Assembly.GetName().Version?.ToString(),
    Binaries = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => new
        {
            Path = Path.GetRelativePath(AppContext.BaseDirectory, path),
            Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
        }).ToArray()
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, "capabilities.json"), json);
Console.WriteLine(json);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int ExperimentInfoQuery(IntPtr output, int capacity);

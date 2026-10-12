using System.Diagnostics;
using System.Security.Cryptography;
using Emgu.CV;

namespace SekaiTools.Tests;

public class NativeRuntimeQualificationTests
{
    [Fact]
    public void QualificationProcessLoadsExpectedNativeRuntime()
    {
        var expected = Environment.GetEnvironmentVariable("SEKAI_TEST_NATIVE_SHA256");
        if (string.IsNullOrEmpty(expected)) return;

        Assert.True(OperatingSystem.IsWindows());
        _ = CvInvoke.NumThreads;
        using var process = Process.GetCurrentProcess();
        var module = Assert.Single(process.Modules.Cast<ProcessModule>(), module =>
            string.Equals(module.ModuleName, "cvextern.dll", StringComparison.OrdinalIgnoreCase));
        using var stream = File.OpenRead(module.FileName);
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(stream)));
    }
}

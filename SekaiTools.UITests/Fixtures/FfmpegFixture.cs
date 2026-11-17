using System.Diagnostics;

namespace SekaiTools.UITests.Fixtures;

public static class FfmpegFixture
{
    public static void Run(string executable, string logPath, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动固定版本 FFmpeg。");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(true);
            process.WaitForExit(5000);
            throw new TimeoutException("测试 FFmpeg 超时，已停止自有进程。");
        }
        File.WriteAllText(logPath, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        if (process.ExitCode != 0) throw new InvalidOperationException($"测试 FFmpeg 退出码 {process.ExitCode}，日志：{logPath}");
    }
}

using System.Diagnostics;
using System.Globalization;

namespace SekaiToolsMedia;

/// <summary>Plays a bounded event using FFmpeg's player and its audio master clock.</summary>
public static class FfplayPreview
{
    public static string? FindExecutable(IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(directory.Trim('"'), "ffplay.exe"));
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }
        return null;
    }

    public static async Task PlayAsync(string executable, string mediaPath, TimeSpan start,
        TimeSpan duration, bool audioOnly, string title, CancellationToken cancellationToken)
    {
        if (start < TimeSpan.Zero || duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-nostats",
                     "-autoexit", "-sync", "audio", "-ss", start.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture),
                     "-t", duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture),
                     "-window_title", title })
            info.ArgumentList.Add(argument);
        if (audioOnly) info.ArgumentList.Add("-nodisp");
        info.ArgumentList.Add("-i");
        info.ArgumentList.Add(Path.GetFullPath(mediaPath));
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("无法启动 ffplay");
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"ffplay 退出代码 {process.ExitCode}" : error.Trim());
    }
}

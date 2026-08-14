using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SekaiToolsMedia;

internal sealed record FfmpegAudioPlan(int StreamCount, bool CopyAudio)
{
    private static readonly HashSet<string> Mp4CopyCompatibleCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac",
        "ac3",
        "alac",
        "eac3",
        "mp3"
    };

    public static FfmpegAudioPlan FromCodecs(IEnumerable<string> codecs)
    {
        var codecList = codecs.ToArray();
        return new FfmpegAudioPlan(
            codecList.Length,
            codecList.Length == 0 || codecList.All(Mp4CopyCompatibleCodecs.Contains));
    }
}

internal static partial class FfmpegAudioInspector
{
    public static async Task<FfmpegAudioPlan> InspectAsync(
        string ffmpegPath,
        string sourceVideo,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourceVideo);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("无法启动 FFmpeg 检测媒体信息");

        using var cancellationRegistration = cancellationToken.Register(
            static state => StopProcess((Process)state!), process);
        var log = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return Parse(log);
    }

    internal static FfmpegAudioPlan Parse(string ffmpegLog)
    {
        var codecs = AudioStreamPattern().Matches(ffmpegLog)
            .Select(match => match.Groups["Codec"].Value);
        return FfmpegAudioPlan.FromCodecs(codecs);
    }

    [GeneratedRegex(@"^\s*Stream #\d+:\d+(?:\[[^\]]+\])?(?:\([^)]+\))?: Audio: (?<Codec>[^,\s]+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AudioStreamPattern();

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (InvalidOperationException)
        {
            // Process has already exited.
        }
        catch (Win32Exception)
        {
            // Process has already exited.
        }
    }
}

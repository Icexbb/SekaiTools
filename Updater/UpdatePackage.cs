using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using SekaiToolsConfiguration;

namespace Updater;

internal static class UpdatePackage
{
    internal static async Task<string?> PrepareAsync(string installDirectory, string stage, HttpMessageHandler handler,
        CancellationToken cancellationToken = default, IProgress<double>? progress = null,
        Func<Version, CancellationToken, Task<bool>>? confirmDownload = null)
    {
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SekaiTools", "1.0"));
        var endpoints = NetworkEndpoints.Current.GitHub;
        using var release = JsonDocument.Parse(await client.GetStringAsync(endpoints.LatestReleaseApiUrl,
            cancellationToken));
        var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version))
            throw new InvalidDataException("发布版本号无效");
        var main = Path.Combine(installDirectory, UpdatePaths.MainExecutable);
        var local = FileVersionInfo.GetVersionInfo(main).FileVersion;
        if (Version.TryParse(local, out var installed) && installed >= version) return null;
        cancellationToken.ThrowIfCancellationRequested();
        if (confirmDownload is not null && !await confirmDownload(version, cancellationToken)) return null;
        cancellationToken.ThrowIfCancellationRequested();

        var url = endpoints.ReleaseDownloadBaseUrl + $"{Uri.EscapeDataString(tag)}/SekaiTools-{version}.7z";
        var checksum = await client.GetStringAsync(url + ".sha256", cancellationToken);
        var expected = checksum.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (expected is not { Length: 64 } || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException("更新包校验文件格式无效");

        Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, "update.7z");
        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, useAsync: true);
            var buffer = new byte[65536];
            long total = 0;
            int lastPercent = -1;
            var length = response.Content.Headers.ContentLength;
            while (true)
            {
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                var count = await input.ReadAsync(buffer, readTimeout.Token);
                if (count == 0) break;
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                total += count;
                if (length is not > 0) continue;
                var percent = (int)(100d * total / length.Value);
                if (percent == lastPercent) continue;
                lastPercent = percent;
                progress?.Report(percent / 100d);
            }
        }

        await using (var stream = File.OpenRead(archive))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("更新包 SHA-256 校验失败");
        }

        var extractor = Path.Combine(installDirectory, "tools", "updater", "7zr.exe");
        if (!File.Exists(extractor)) extractor = Path.Combine(installDirectory, "7zr.exe");
        using var unpack = UpdatePaths.Start(extractor, stage, "x", archive, "-y", "-o" + stage);
        try
        {
            await unpack.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!unpack.HasExited) unpack.Kill(entireProcessTree: true);
            await unpack.WaitForExitAsync();
            throw;
        }

        if (unpack.ExitCode != 0) throw new InvalidDataException($"解压失败，退出码 {unpack.ExitCode}");
        File.Delete(archive);
        var executable = Path.Combine(stage, "SekaiTools", UpdatePaths.MainExecutable);
        if (!File.Exists(executable) ||
            !Version.TryParse(FileVersionInfo.GetVersionInfo(executable).FileVersion, out var extracted) ||
            extracted != version)
            throw new InvalidDataException("更新包里的主程序缺失或版本不符");

        return version.ToString();
    }
}

using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiDataFetch;

public static class VoiceCache
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, int> ProtectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ILogger Log = Logger.CreateLogger("VoiceCache");
    private const long MiB = 1024 * 1024;

    // 0.1% of C: capacity, bounded to 128 MiB–2 GiB and at most 5% of available space.
    public static long CalculateLimit(long totalBytes, long availableBytes) =>
        Math.Min(Math.Clamp(totalBytes / 1000, 128 * MiB, 2048 * MiB), Math.Max(0, availableBytes / 20));

    public static long AutomaticLimit(string cacheDirectory)
    {
        try
        {
            var drive = new DriveInfo(OperatingSystem.IsWindows() ? @"C:\" : Path.GetPathRoot(Path.GetFullPath(cacheDirectory))!);
            return CalculateLimit(drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.LogWarning(ex, "读取 C 盘容量失败，语音缓存使用 128 MiB 上限");
            return 128 * MiB;
        }
    }

    public static IDisposable Protect(string path)
    {
        path = Path.GetFullPath(path);
        lock (Gate) ProtectedPaths[path] = ProtectedPaths.GetValueOrDefault(path) + 1;
        return new Protection(path);
    }

    private sealed class Protection(string path) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            lock (Gate)
            {
                if (_disposed) return;
                _disposed = true;
                if (--ProtectedPaths[path] == 0) ProtectedPaths.Remove(path);
            }
        }
    }

    public static void Trim(string cacheDirectory, long limitBytes, string? keepPath = null)
    {
        var directory = Path.Combine(cacheDirectory, "voice");
        lock (Gate)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                var files = new DirectoryInfo(directory).GetFiles("*.mp3")
                    .Where(file => file.Name.Length == 68 && file.Name[..64].All(Uri.IsHexDigit))
                    .OrderBy(file => file.LastWriteTimeUtc).ToArray();
                var size = files.Sum(file => file.Length);
                foreach (var file in files)
                {
                    if (size <= limitBytes) break;
                    if (string.Equals(file.FullName, keepPath, StringComparison.OrdinalIgnoreCase) || ProtectedPaths.ContainsKey(file.FullName)) continue;
                    try
                    {
                        var length = file.Length;
                        file.Delete();
                        size -= length;
                        Log.LogInformation("清理普通语音缓存：{File}，{Bytes} 字节，上限 {LimitBytes} 字节", file.Name, length, limitBytes);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { Log.LogWarning(ex, "无法清理语音缓存：{File}", file.Name); }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Log.LogWarning(ex, "扫描语音缓存失败：{Directory}", directory); }
        }
    }
}

namespace SekaiToolsMedia;

public sealed record SuppressionStorageCheck(string TemporaryDirectory, long? AvailableBytes, long RecommendedBytes)
{
    private const long GiB = 1024L * 1024 * 1024;

    public static string TemporaryRoot => Path.Combine(Path.GetTempPath(), "SekaiTools", "Suppression");

    public bool NeedsWarning => AvailableBytes == null || AvailableBytes < RecommendedBytes;

    public string WarningMessage =>
        $"临时目录：{TemporaryDirectory}\n" +
        (AvailableBytes is { } available ? $"当前可用：{available / (double)GiB:F2} GiB\n" : "无法获取临时目录的可用空间。\n") +
        $"建议可用空间：至少 {RecommendedBytes / (double)GiB:F2} GiB\n\n" +
        "按源视频大小的 2 倍（至少 1 GiB），另加 1 GiB 余量进行预估。CRF 编码后的大小无法提前精确确定，实际用量可能更高。\n\n" +
        "空间不足可能导致压制失败。建议先清理临时磁盘空间，再开始压制。是否仍要继续？";

    public static SuppressionStorageCheck Inspect(string sourceVideo)
    {
        var recommended = EstimateRecommendedBytes(new FileInfo(sourceVideo).Length);
        long? available = null;
        try
        {
            available = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(TemporaryRoot))!).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An unavailable disk-space query must still surface a warning to the user.
        }

        return new SuppressionStorageCheck(TemporaryRoot, available, recommended);
    }

    internal static long EstimateRecommendedBytes(long sourceBytes) =>
        sourceBytes > (long.MaxValue - GiB) / 2 ? long.MaxValue : Math.Max(sourceBytes * 2, GiB) + GiB;
}

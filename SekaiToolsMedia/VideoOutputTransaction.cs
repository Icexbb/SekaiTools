namespace SekaiToolsMedia;

internal sealed class VideoOutputTransaction : IDisposable
{
    private readonly bool _overwriteExisting;
    private bool _committed;

    public VideoOutputTransaction(string targetPath, bool overwriteExisting)
    {
        TargetPath = Path.GetFullPath(targetPath);
        _overwriteExisting = overwriteExisting;
        if (File.Exists(TargetPath) && !overwriteExisting)
            throw new IOException($"输出文件已存在: {TargetPath}");

        // Keep in-progress videos outside output folders watched by backup/sync software.
        var directory = SuppressionStorageCheck.TemporaryRoot;
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(TargetPath);
        do
        {
            TemporaryPath = Path.Combine(
                directory, $"{Guid.NewGuid():N}.partial{extension}");
        } while (File.Exists(TemporaryPath));
    }

    public string TargetPath { get; }

    public string TemporaryPath { get; } = "";

    public void Dispose()
    {
        if (!_committed && File.Exists(TemporaryPath))
            File.Delete(TemporaryPath);
    }

    public void Commit()
    {
        if (!File.Exists(TemporaryPath))
            throw new FileNotFoundException("压制临时文件不存在", TemporaryPath);

        File.Move(TemporaryPath, TargetPath, _overwriteExisting);
        _committed = true;
    }
}

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

        var directory = Path.GetDirectoryName(TargetPath)
                        ?? throw new ArgumentException("输出路径缺少目录", nameof(targetPath));
        var fileName = Path.GetFileNameWithoutExtension(TargetPath);
        var extension = Path.GetExtension(TargetPath);
        do
        {
            TemporaryPath = Path.Combine(
                directory, $".{fileName}.{Guid.NewGuid():N}.partial{extension}");
        } while (File.Exists(TemporaryPath));
    }

    public string TargetPath { get; }

    public string TemporaryPath { get; private set; } = "";

    public void Commit()
    {
        if (!File.Exists(TemporaryPath))
            throw new FileNotFoundException("压制临时文件不存在", TemporaryPath);

        File.Move(TemporaryPath, TargetPath, _overwriteExisting);
        _committed = true;
    }

    public void Dispose()
    {
        if (!_committed && File.Exists(TemporaryPath))
            File.Delete(TemporaryPath);
    }
}

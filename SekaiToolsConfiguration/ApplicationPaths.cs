namespace SekaiToolsConfiguration;

/// <summary>Production paths remain unchanged; an explicit marked root isolates UI test processes.</summary>
public sealed class ApplicationPaths
{
    public const string TestRootVariable = "SEKAI_TOOLS_TEST_ROOT";
    public const string TestRootMarker = ".sekai-ui-test-root";
    public const string TestRootMarkerContent = "SekaiTools isolated UI test root v1";
    private static readonly Lazy<ApplicationPaths> CurrentValue = new(() => Create(
        Environment.GetEnvironmentVariable(TestRootVariable)));

    private ApplicationPaths(string dataRoot, string localRoot, string? testRoot)
    {
        DataRoot = dataRoot;
        LocalRoot = localRoot;
        TestRoot = testRoot;
    }

    public static ApplicationPaths Current => CurrentValue.Value;
    public string DataRoot { get; }
    public string LocalRoot { get; }
    public string? TestRoot { get; }
    public bool IsIsolated => TestRoot is not null;
    public string VoiceCache => Path.Combine(LocalRoot, "VoiceCache");

    public static ApplicationPaths Create(string? testRoot)
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SekaiTools");
        if (testRoot is null) return new ApplicationPaths(data, local, null);
        if (string.IsNullOrWhiteSpace(testRoot) || !Path.IsPathFullyQualified(testRoot))
            throw new InvalidDataException("UI 测试数据根必须是非空绝对路径。");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(testRoot));
        foreach (var protectedRoot in new[] { data, local })
        {
            if (ContainsPath(root, protectedRoot) || ContainsPath(protectedRoot, root))
                throw new InvalidDataException("UI 测试目录不能与正式数据目录重叠。");
        }

        // Reject junctions/symlinks rather than allowing marked paths to escape into real data.
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("UI 测试根及其父目录必须存在且不能是链接。");
        }

        var marker = Path.Combine(root, TestRootMarker);
        if (!File.Exists(marker) || File.ReadAllText(marker).Trim() != TestRootMarkerContent)
            throw new InvalidDataException("UI 测试目录缺少有效的隔离标记。");
        ValidateContents(root);
        return new ApplicationPaths(Path.Combine(root, "DataRoot"), Path.Combine(root, "LocalRoot"), root);
    }

    private static void ValidateContents(string root)
    {
        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("UI 测试数据根内不能包含链接。");
            if (entry is DirectoryInfo directory) ValidateContents(directory.FullName);
        }
    }

    public static bool ContainsPath(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = Path.EndsInDirectorySeparator(normalizedRoot) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.Equals(normalizedRoot, comparison) || normalizedPath.StartsWith(prefix, comparison);
    }
}

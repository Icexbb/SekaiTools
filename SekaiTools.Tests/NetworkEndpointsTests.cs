using SekaiToolsConfiguration;

namespace SekaiTools.Tests;

public class NetworkEndpointsTests
{
    [Fact]
    public void 发布输出包含可编辑的网络端点配置()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, NetworkEndpoints.FileName);

        Assert.True(File.Exists(configPath), $"缺少网络端点配置: {configPath}");
    }

    [Fact]
    public void 网络端点配置可以加载并通过校验()
    {
        var endpoints = NetworkEndpoints.Load();

        Assert.True(NetworkEndpoints.RepositoryUri.IsAbsoluteUri);
        Assert.NotEmpty(endpoints.DefaultDataSources);
        Assert.All(endpoints.DefaultDataSources,
            source => Assert.Contains("{type}", source.SourceTemplate, StringComparison.Ordinal));
    }

    [Fact]
    public void 非Http端点会被拒绝()
    {
        var configPath = Path.GetTempFileName();
        try
        {
            var validEndpoints = NetworkEndpoints.Load();
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, NetworkEndpoints.FileName))
                .Replace(validEndpoints.GitHub.RepositoryUrl, "file:///SekaiTools", StringComparison.Ordinal);
            File.WriteAllText(configPath, json);

            Assert.Throws<InvalidDataException>(() => NetworkEndpoints.Load(configPath));
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void 产品CSharp源码不再硬编码Https端点()
    {
        var root = FindRepositoryRoot();
        var productProjects = new[]
        {
            "SekaiToolsBase",
            "SekaiToolsConfiguration",
            "SekaiToolsCore",
            "SekaiToolsInfrastructure",
            "SekaiToolsMedia",
            "SekaiToolsSubtitles",
            "SekaiDataFetch",
            "SekaiToolsGUI",
            "Updater"
        };

        var offenders = productProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("https://", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SekaiTools.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 SekaiTools.sln");
    }
}

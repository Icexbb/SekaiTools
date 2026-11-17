using SekaiToolsConfiguration;
using System.Text.Json;
using Xunit;

namespace SekaiTools.Tests;

public sealed class ApplicationPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SekaiTools-PathTests-" + Guid.NewGuid().ToString("N"));

    public ApplicationPathsTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void 正式路径保持兼容且未启用隔离()
    {
        var paths = ApplicationPaths.Create(null);
        Assert.False(paths.IsIsolated);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools"), paths.DataRoot);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SekaiTools", "VoiceCache"), paths.VoiceCache);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative-test-root")]
    public void 无效隔离路径不能回退正式数据(string root) =>
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Create(root));

    [Fact]
    public void 未标记目录不能启用测试隔离() =>
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Create(_root));

    [Fact]
    public void 正式目录及其子目录不能成为测试根()
    {
        var production = ApplicationPaths.Create(null);
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Create(production.DataRoot));
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Create(Path.GetPathRoot(production.DataRoot)));
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Create(Path.Combine(production.LocalRoot, "test")));
    }

    [Fact]
    public void 标记目录将数据和语音缓存隔离到同一根下()
    {
        File.WriteAllText(Path.Combine(_root, ApplicationPaths.TestRootMarker), ApplicationPaths.TestRootMarkerContent);
        var paths = ApplicationPaths.Create(_root);
        Assert.True(paths.IsIsolated);
        Assert.True(ApplicationPaths.ContainsPath(_root, paths.DataRoot));
        Assert.True(ApplicationPaths.ContainsPath(_root, paths.VoiceCache));
        Assert.False(ApplicationPaths.ContainsPath(_root, _root + "-sibling"));
    }

    [Fact]
    public void 隔离模式缺少端点文件时不能回退正式服务()
    {
        var paths = MarkedPaths();
        Assert.Throws<InvalidDataException>(() => NetworkEndpoints.Load(paths: paths));
    }

    [Fact]
    public void 隔离模式拒绝正式外网端点和根外配置()
    {
        var paths = MarkedPaths();
        var file = Path.Combine(_root, NetworkEndpoints.FileName);
        File.WriteAllText(file, JsonSerializer.Serialize(NetworkEndpoints.Load()));
        Assert.Throws<InvalidDataException>(() => NetworkEndpoints.Load(file, paths));
        Assert.Throws<InvalidDataException>(() => NetworkEndpoints.Load(Path.Combine(_root + "-other", NetworkEndpoints.FileName), paths));
    }

    private ApplicationPaths MarkedPaths()
    {
        File.WriteAllText(Path.Combine(_root, ApplicationPaths.TestRootMarker), ApplicationPaths.TestRootMarkerContent);
        return ApplicationPaths.Create(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

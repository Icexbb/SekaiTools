using Updater;

namespace SekaiTools.Tests;

public class UpdateProxySettingsTests
{
    [Theory]
    [InlineData(1, "http")]
    [InlineData(2, "socks5")]
    public void InheritedSettingsOverrideSavedSettingsAndUseCorrectProtocol(int type, string scheme)
    {
        var settings = UpdateProxySettings.Load(
            $$"""{"ProxyType":{{type}},"ProxyHost":"::1","ProxyPort":7890}""", "unused-setting.json");
        using var handler = settings.CreateHandler();
        var address = handler.Proxy!.GetProxy(new Uri("https://example.com"))!;
        Assert.True(handler.UseProxy);
        Assert.Equal(scheme, address.Scheme);
        Assert.Equal("[::1]", address.Host);
        Assert.Equal(7890, address.Port);
    }

    [Fact]
    public void StandaloneUpdaterReadsMainAppSettingsFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"ProxyType":1,"ProxyHost":"localhost","ProxyPort":8080,"OtherSetting":true}""");
            var settings = UpdateProxySettings.Load(null, path);
            Assert.Equal(new UpdateProxySettings(1, "localhost", 8080), settings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingSettingsKeepDefaultProxyBehavior()
    {
        var settings = UpdateProxySettings.Load(null, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "setting.json"));
        using var handler = settings.CreateHandler();
        Assert.Equal(0, settings.ProxyType);
        Assert.Null(handler.Proxy);
        Assert.True(handler.UseProxy);
    }
}

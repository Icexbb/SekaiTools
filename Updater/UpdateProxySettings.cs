using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Updater;

internal sealed record UpdateProxySettings(int ProxyType = 0, string ProxyHost = "127.0.0.1", int ProxyPort = 1080)
{
    internal const string EnvironmentVariable = "SEKAI_TOOLS_UPDATE_PROXY";

    internal static UpdateProxySettings Load(string? inheritedSettings, string settingFilePath)
    {
        if (!string.IsNullOrEmpty(inheritedSettings))
            return Parse(inheritedSettings);

        try
        {
            return Parse(File.ReadAllText(settingFilePath));
        }
        catch (FileNotFoundException)
        {
            return new();
        }
        catch (DirectoryNotFoundException)
        {
            return new();
        }
    }

    private static UpdateProxySettings Parse(string json) =>
        JsonSerializer.Deserialize<UpdateProxySettings>(json)
        ?? throw new InvalidDataException("代理设置无效");

    internal HttpClientHandler CreateHandler()
    {
        if (ProxyType == 0) return new HttpClientHandler();
        var scheme = ProxyType switch
        {
            1 => "http",
            2 => "socks5",
            _ => throw new InvalidDataException("未知的代理类型")
        };
        if (string.IsNullOrWhiteSpace(ProxyHost) || ProxyPort is < 1 or > 65535)
            throw new InvalidDataException("代理地址或端口无效");
        return new HttpClientHandler
        {
            Proxy = new WebProxy(new UriBuilder(scheme, ProxyHost, ProxyPort).Uri),
            UseProxy = true
        };
    }
}

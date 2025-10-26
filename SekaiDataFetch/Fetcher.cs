using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using SekaiDataFetch.Source;
using SekaiToolsBase;

namespace SekaiDataFetch;

public class Fetcher
{
    public SourceList SourceList => SourceList.Instance;
    private Proxy UserProxy { get; set; } = Proxy.None;

    public static Fetcher Instance { get; } = new();

    public void SetSource(SourceData sourceData)
    {
        SourceList.SourceData = sourceData;
    }

    public void SetProxy(Proxy proxy)
    {
        UserProxy = proxy;
    }

    private HttpMessageHandler GetHttpHandler()
    {
        return UserProxy.ProxyType switch
        {
            Proxy.Type.None => new HttpClientHandler(),
            Proxy.Type.System => new HttpClientHandler(),
            Proxy.Type.Http => new HttpClientHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            Proxy.Type.Socks5 => new SocketsHttpHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public async Task<string> Fetch(string url, string defaultResult = "{}")
    {
        return await TryGet();

        async Task<string> TryGet(int time = 5)
        {
            Logger.Log($"{GetType().Name} Fetching data from {url}");
            try
            {
                return await Get();
            }
            catch (HttpRequestException e)
            {
                Logger.Log(
                    $"{GetType().Name} Failed to fetch data from {url}. Retrying {time} times. Error: {e.Message}",
                    LogLevel.Error);
                if (time > 0) return await TryGet(time - 1);
                if (Debugger.IsAttached) throw;
                return defaultResult;
            }
        }

        async Task<string> Get()
        {
            using var client = new HttpClient(GetHttpHandler());
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
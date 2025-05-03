using System.Diagnostics;
using System.Net;
using SekaiDataFetch.Source;

namespace SekaiDataFetch;

public class Fetcher
{
    public SourceList Source { get; } = new();
    private Proxy UserProxy { get; set; } = Proxy.None;

    public void SetSource(SourceType sourceType)
    {
        Source.SetSource(sourceType);
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
            Console.WriteLine($"GET {url}");
            try
            {
                return await Get();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"GET {url} Error: " + (e.InnerException?.Message ?? e.Message));
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
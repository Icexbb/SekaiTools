using System.Runtime.CompilerServices;
using System.Net;
using Microsoft.Extensions.Logging;
using SekaiDataFetch.Source;
using SekaiToolsBase;

[assembly: InternalsVisibleTo("SekaiTools.Tests")]

namespace SekaiDataFetch;

public class Fetcher : IDisposable
{
    private readonly object _clientLock = new();
    private readonly Func<Proxy, HttpMessageHandler> _handlerFactory;
    private ClientEntry? _client;
    private bool _disposed;

    public Fetcher() : this(GetHttpHandler) { }
    internal Fetcher(Func<Proxy, HttpMessageHandler> handlerFactory) => _handlerFactory = handlerFactory;
    public SourceList SourceList => SourceList.Instance;
    private Proxy UserProxy { get; set; } = Proxy.None;

    public static Fetcher Instance { get; } = new();

    public void SetSource(SourceData sourceData)
    {
        SourceList.SourceData = sourceData;
    }

    public void SetProxy(Proxy proxy)
    {
        lock (_clientLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Snapshot mutable settings; requests already in flight retain their client.
            var snapshot = Proxy.None;
            snapshot.SetProxy(proxy);
            UserProxy = snapshot;
        }
    }

    private static HttpMessageHandler GetHttpHandler(Proxy proxy)
    {
        return proxy.ProxyType switch
        {
            Proxy.Type.None => new HttpClientHandler(),
            Proxy.Type.System => new HttpClientHandler(),
            Proxy.Type.Http => new HttpClientHandler
            {
                Proxy = new WebProxy(proxy.Host, proxy.Port),
                UseProxy = true
            },
            Proxy.Type.Socks5 => new SocketsHttpHandler
            {
                Proxy = new WebProxy(proxy.Host, proxy.Port),
                UseProxy = true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private ClientEntry AcquireClient()
    {
        lock (_clientLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var key = UserProxy.GetProxyString();
            if (_client == null || _client.Key != key)
            {
                var next = new ClientEntry(key, new HttpClient(_handlerFactory(UserProxy))
                { Timeout = TimeSpan.FromSeconds(10) });
                Retire(_client);
                _client = next;
            }
            _client.Users++;
            return _client;
        }
    }

    private void Release(ClientEntry entry)
    {
        lock (_clientLock)
        {
            entry.Users--;
            if (entry.Retired && entry.Users == 0) entry.Client.Dispose();
        }
    }

    private static void Retire(ClientEntry? entry)
    {
        if (entry == null) return;
        entry.Retired = true;
        if (entry.Users == 0) entry.Client.Dispose();
    }

    public void Dispose()
    {
        lock (_clientLock)
        {
            if (_disposed) return;
            _disposed = true;
            Retire(_client);
            _client = null;
        }
    }

    private sealed class ClientEntry(string key, HttpClient client)
    {
        public string Key { get; } = key;
        public HttpClient Client { get; } = client;
        public int Users { get; set; }
        public bool Retired { get; set; }
    }

    public async Task<byte[]> FetchBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        var entry = AcquireClient();
        try
        {
            using var response = await entry.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { Release(entry); }
    }

    public async Task<string> FetchOnceAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = AcquireClient();
        try
        {
            using var response = await entry.Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { Release(entry); }
    }

    public async Task<string> Fetch(string url, CancellationToken cancellationToken = default)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await FetchOnceAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (attempt < attempts && !cancellationToken.IsCancellationRequested &&
                (error is TaskCanceledException || error is HttpRequestException request &&
                    (request.StatusCode == null || request.StatusCode is HttpStatusCode.RequestTimeout or
                        HttpStatusCode.TooManyRequests || (int)request.StatusCode >= 500)))
            {
                Logger.Log($"{GetType().Name} 请求失败，第 {attempt}/{attempts} 次，稍后重试：{error.Message}", LogLevel.Warning);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

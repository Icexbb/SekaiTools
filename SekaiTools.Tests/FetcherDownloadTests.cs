using System.Net;
using SekaiDataFetch;
using SekaiToolsBase;

namespace SekaiTools.Tests;

public class FetcherDownloadTests
{
    private const string Url = "https://download.invalid/story.json";

    [Fact]
    public async Task ReusesClientAndDoesNotRetryMissingFile()
    {
        var created = 0;
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var fetcher = new Fetcher(_ =>
        {
            created++;
            return handler;
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.Fetch(Url));
        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchOnceAsync(Url));
        Assert.Equal(1, created);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task RetriesTransientFailureAndStopsAtThreeAttempts()
    {
        var handler = new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var fetcher = new Fetcher(_ => handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.Fetch(Url));
        Assert.Equal(3, handler.Requests);
    }

    [Fact]
    public async Task CancellationReachesRequestWithoutRetry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Success();
        });
        using var fetcher = new Fetcher(_ => handler);
        using var cancel = new CancellationTokenSource();
        var task = fetcher.Fetch(Url, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task ProxySwitchRetiresClientOnlyAfterInflightRequestFinishes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new Handler(async (_, _) =>
        {
            entered.SetResult();
            await finish.Task;
            return Success();
        });
        var next = new Handler((_, _) => Task.FromResult(Success()));
        using var fetcher = new Fetcher(proxy => proxy.ProxyType == Proxy.Type.None ? old : next);
        var inflight = fetcher.FetchOnceAsync(Url);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fetcher.SetProxy(new Proxy("localhost", 8080, Proxy.Type.Http));
        Assert.Equal("{}", await fetcher.FetchOnceAsync(Url));
        Assert.False(old.Disposed);
        finish.SetResult();
        Assert.Equal("{}", await inflight);
        Assert.True(old.Disposed);
        Assert.False(next.Disposed);
    }

    [Fact]
    public async Task FailedOrCanceledFetchPreservesExistingFileAndSuccessfulFetchReplacesIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SekaiTools-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "story.json");
        try
        {
            await File.WriteAllTextAsync(path, "existing");
            using (var missing = new Fetcher(_ =>
                       new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))))
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => missing.FetchToFile(Url, path));
            }

            Assert.Equal("existing", await File.ReadAllTextAsync(path));
            using var fetcher = new Fetcher(_ => new Handler((_, _) => Task.FromResult(Success())));
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetcher.FetchToFile(Url, path, cancel.Token));
            Assert.Equal("existing", await File.ReadAllTextAsync(path));
            await fetcher.FetchToFile(Url, path);
            Assert.Equal("{}", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static HttpResponseMessage Success()
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            return send(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
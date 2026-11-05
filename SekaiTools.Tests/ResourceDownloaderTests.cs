using System.Net;
using SekaiToolsInfrastructure.Resources;

namespace SekaiTools.Tests;

public class ResourceDownloaderTests
{
    [Fact]
    public async Task HeaderTimeoutIsBoundedAndRetried()
    {
        var requests = 0;
        var downloader = new ResourceDownloader(() => new WaitingHeaderHandler(() => requests++),
            headerTimeout: TimeSpan.FromMilliseconds(30), retryDelay: TimeSpan.Zero);
        var error = await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(
            "http://localhost/resource", "headers", 0,
            (body, token) => body.CopyToAsync(Stream.Null, token), null, default));
        Assert.Equal(3, requests);
        Assert.IsType<TimeoutException>(error.InnerException);
    }

    private sealed class WaitingHeaderHandler(Action started) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            started();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    [Fact]
    public async Task ContinuousSlowBodyCanOutlastHeaderTimeout()
    {
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SlowStream(8, TimeSpan.FromMilliseconds(30)))
        });
        var downloader = new ResourceDownloader(() => handler, TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(2), TimeSpan.Zero);
        using var target = new MemoryStream();
        await downloader.DownloadAsync("http://localhost/resource", "slow", 8,
            (body, token) => body.CopyToAsync(target, token), null, default);
        Assert.Equal(8, target.Length);
        Assert.Equal(1, handler.Requests);
        Assert.True(handler.Disposed);
    }

    [Fact]
    public async Task StalledBodyStopsAfterThreeAttempts()
    {
        var handlers = new List<StubHandler>();
        var downloader = new ResourceDownloader(() =>
        {
            var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowStream(1, Timeout.InfiniteTimeSpan))
            });
            handlers.Add(handler);
            return handler;
        }, idleTimeout: TimeSpan.FromMilliseconds(30), retryDelay: TimeSpan.Zero);
        var error = await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(
            "http://localhost/resource", "stalled.bin", 1,
            (body, token) => body.CopyToAsync(Stream.Null, token), null, default));
        Assert.Contains("stalled.bin", error.Message);
        Assert.IsType<TimeoutException>(error.InnerException);
        Assert.Equal(3, handlers.Count);
        Assert.All(handlers, handler => Assert.True(handler.Disposed));
    }

    [Fact]
    public async Task CancellationInterruptsBodyWithoutRetry()
    {
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SlowStream(1, Timeout.InfiniteTimeSpan))
        });
        using var cancellation = new CancellationTokenSource();
        var downloader = new ResourceDownloader(() => handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(
            "http://localhost/resource", "cancel", 1, async (body, token) =>
            {
                cancellation.Cancel();
                await body.CopyToAsync(Stream.Null, token);
            }, null, cancellation.Token));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task TransientServerFailureRetriesAndReportsAttempt()
    {
        var requests = 0;
        var progress = new CaptureProgress();
        var downloader = new ResourceDownloader(() => new StubHandler(() =>
            ++requests < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) :
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }),
            retryDelay: TimeSpan.Zero);
        await downloader.DownloadAsync("http://localhost/resource", "retry", 3,
            (body, token) => body.CopyToAsync(Stream.Null, token), progress, default);
        Assert.Equal(3, requests);
        Assert.Contains(progress.Items, item => item.Attempt == 3 && item.BytesReceived == 3);
    }

    [Fact]
    public async Task MissingResourceDoesNotRetry()
    {
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<HttpRequestException>(() => new ResourceDownloader(() => handler)
            .DownloadAsync("http://localhost/resource", "missing", 0,
                (body, token) => body.CopyToAsync(Stream.Null, token), null, default));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task CancellationInterruptsRetryDelay()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(() =>
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ResourceDownloader(() => handler)
            .DownloadAsync("http://localhost/resource", "retry", 0,
                (body, token) => body.CopyToAsync(Stream.Null, token), null, cancellation.Token));
        Assert.Equal(1, handler.Requests);
    }

    private sealed class CaptureProgress : IProgress<ResourceDownloadProgress>
    {
        public List<ResourceDownloadProgress> Items { get; } = [];
        public void Report(ResourceDownloadProgress value) => Items.Add(value);
    }

    private sealed class StubHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(response());
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class SlowStream(int chunks, TimeSpan delay) : Stream
    {
        private int _remaining = chunks;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining == 0) return 0;
            await Task.Delay(delay, cancellationToken);
            buffer.Span[0] = 1;
            _remaining--;
            return 1;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

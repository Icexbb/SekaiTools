using System.Net;

namespace SekaiToolsInfrastructure.Resources;

public record ResourceDownloadProgress(string FileName, long BytesReceived, long TotalBytes, int Attempt);

internal sealed class ResourceDownloader(
    Func<HttpMessageHandler> handlerFactory,
    TimeSpan? headerTimeout = null,
    TimeSpan? idleTimeout = null,
    TimeSpan? retryDelay = null)
{
    internal async Task DownloadAsync(string url, string name, long size,
        Func<Stream, CancellationToken, Task> consume,
        IProgress<ResourceDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new HttpClient(handlerFactory()) { Timeout = Timeout.InfiniteTimeSpan };
                using var headers = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                headers.CancelAfter(headerTimeout ?? TimeSpan.FromSeconds(120));
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, headers.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"等待资源响应超时: {name}");
                }

                using (response)
                {
                    headers.CancelAfter(Timeout.InfiniteTimeSpan);
                    cancellationToken.ThrowIfCancellationRequested();
                    response.EnsureSuccessStatusCode();
                    await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var source = new IdleReadStream(body, idleTimeout ?? TimeSpan.FromSeconds(120),
                        bytes => progress?.Report(new ResourceDownloadProgress(name, bytes, size, attempt)));
                    progress?.Report(new ResourceDownloadProgress(name, 0, size, attempt));
                    await consume(source, cancellationToken);
                }
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsTransient(exception))
            {
                if (attempt >= 3)
                    throw new IOException($"资源下载失败（已尝试 3 次）: {name}。{exception.Message}", exception);
                SekaiToolsBase.Logger.Log($"资源下载准备重试: {name}, 已尝试={attempt}, 原因={exception.Message}",
                    Microsoft.Extensions.Logging.LogLevel.Warning);
                await Task.Delay((retryDelay ?? TimeSpan.FromSeconds(2)) * attempt, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new HttpRequestException($"资源下载失败: {name}。{exception.Message}", exception, exception.StatusCode);
            }
        }
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        HttpRequestException http => http.StatusCode is null or HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests || (int)http.StatusCode.Value >= 500,
        TimeoutException => true,
        IOException => true,
        _ => false
    };

    private sealed class IdleReadStream(Stream inner, TimeSpan timeout, Action<long> report) : Stream
    {
        private long _received;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(timeout);
            int count;
            try
            {
                count = await inner.ReadAsync(buffer, idle.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("资源下载长时间未收到数据");
            }
            _received += count;
            report(_received);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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

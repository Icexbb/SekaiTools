using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiDataFetch.Source;

public static class DownloadSources
{
    public static async Task<SourceData[]> RankAsync(Fetcher fetcher, IReadOnlyList<SourceData> sources,
        CancellationToken token = default)
    {
        if (sources.Count < 2) return sources.ToArray();
        var measurements = await Task.WhenAll(sources.Select(async source =>
        {
            var catalog = MeasureAsync(new SourceList(source).Cards, requireSuccess: true);
            var storage = MeasureAsync(source.StorageBaseUrl, requireSuccess: false);
            return (Source: source, Score: await catalog.ConfigureAwait(false) + await storage.ConfigureAwait(false));
        })).ConfigureAwait(false);
        return measurements.OrderBy(item => item.Score).Select(item => item.Source).ToArray();

        async Task<double> MeasureAsync(string url, bool requireSuccess)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var started = Stopwatch.GetTimestamp();
            try
            {
                var status = await fetcher.ProbeAsync(url, timeout.Token).ConfigureAwait(false);
                var usable = requireSuccess
                    ? (int)status is >= 200 and < 300 || status == HttpStatusCode.MethodNotAllowed
                    : (int)status < 500 && status is not (HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout);
                if (usable)
                    return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
            catch (Exception error) when (!token.IsCancellationRequested &&
                                          error is HttpRequestException or OperationCanceledException or InvalidOperationException or UriFormatException)
            {
                Logger.Log($"数据源测速失败：{url} · {error.Message}", LogLevel.Debug);
            }
            return 2_000;
        }
    }

    public static async Task<T> TryAsync<TSource, T>(IEnumerable<TSource> sources,
        Func<TSource, CancellationToken, Task<T>> attempt, CancellationToken token)
    {
        var failures = new List<Exception>();
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return await attempt(source, token);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException)
            {
                token.ThrowIfCancellationRequested();
                failures.Add(error);
                Logger.Log($"下载源请求失败：{error.Message}", LogLevel.Warning);
            }
        }
        token.ThrowIfCancellationRequested();
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failures[0]);
        throw new InvalidDataException("所有下载源均不可用。", new AggregateException(failures));
    }
}

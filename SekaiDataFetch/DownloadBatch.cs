using System.Net;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiDataFetch;

public enum DownloadStatus
{
    Waiting,
    Downloading,
    Succeeded,
    Failed,
    Stopped,
    Skipped
}

public enum ExistingFilePolicy
{
    Skip,
    Replace
}

public sealed record DownloadJob(Guid Id, string Title, string Url, string SavePath, string SourceName);

public sealed record DownloadUpdate(DownloadJob Job, DownloadStatus Status, string? Error = null);

public sealed record DownloadBatchResult(int Succeeded, int Failed, int Stopped, int Skipped);

/// <summary>A fixed batch; new UI additions belong to the next batch.</summary>
public sealed class DownloadBatch
{
    private readonly Func<DownloadJob, ExistingFilePolicy, CancellationToken, Task> _download;

    public DownloadBatch(Fetcher fetcher) : this((job, policy, token) =>
        fetcher.FetchToFile(job.Url, job.SavePath, token, policy == ExistingFilePolicy.Replace))
    {
    }

    internal DownloadBatch(Func<DownloadJob, ExistingFilePolicy, CancellationToken, Task> download)
    {
        _download = download;
    }

    public async Task<DownloadBatchResult> RunAsync(IReadOnlyList<DownloadJob> jobs,
        ExistingFilePolicy policy, Action<DownloadUpdate> report, CancellationToken cancellationToken = default)
    {
        if (jobs.Select(job => job.Id).Distinct().Count() != jobs.Count)
            throw new ArgumentException("Duplicate task identity", nameof(jobs));
        var conflicts = jobs.GroupBy(job => Path.GetFullPath(job.SavePath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).SelectMany(group => group).Select(job => job.Id).ToHashSet();
        var states = new DownloadStatus?[jobs.Count];
        var cursor = -1;
        await Task.WhenAll(Enumerable.Range(0, Math.Min(3, jobs.Count)).Select(_ => Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var index = Interlocked.Increment(ref cursor);
                if (index >= jobs.Count) break;
                var job = jobs[index];
                if (conflicts.Contains(job.Id))
                {
                    Finish(DownloadStatus.Failed, "保存文件名冲突，请移除同名任务后重试。");
                    continue;
                }

                if (policy == ExistingFilePolicy.Skip && File.Exists(job.SavePath))
                {
                    Finish(DownloadStatus.Skipped);
                    continue;
                }

                report(new DownloadUpdate(job, DownloadStatus.Downloading));
                try
                {
                    await _download(job, policy, cancellationToken).ConfigureAwait(false);
                    Finish(DownloadStatus.Succeeded);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Finish(DownloadStatus.Stopped);
                }
                catch (IOException) when (policy == ExistingFilePolicy.Skip && File.Exists(job.SavePath))
                {
                    Finish(DownloadStatus.Skipped);
                }
                catch (Exception error)
                {
                    Logger.Log($"下载 {job.Title} 失败：{error}", LogLevel.Error);
                    Finish(DownloadStatus.Failed, DescribeError(error));
                }

                void Finish(DownloadStatus status, string? error = null)
                {
                    states[index] = status;
                    report(new DownloadUpdate(job, status, error));
                }
            }
        }))).ConfigureAwait(false);
        for (var index = 0; index < jobs.Count; index++)
            if (states[index] == null)
            {
                states[index] = DownloadStatus.Stopped;
                report(new DownloadUpdate(jobs[index], DownloadStatus.Stopped));
            }

        return new DownloadBatchResult(states.Count(state => state == DownloadStatus.Succeeded),
            states.Count(state => state == DownloadStatus.Failed),
            states.Count(state => state == DownloadStatus.Stopped),
            states.Count(state => state == DownloadStatus.Skipped));
    }

    public static string DescribeError(Exception error)
    {
        return error switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "源站未找到此文件（404），可刷新列表或更换数据源。",
            HttpRequestException
            {
                StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
            } => "源站拒绝访问，请检查数据源。",
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "源站请求过于频繁，请稍后重试。",
            OperationCanceledException => "请求超时，请检查网络后重试。",
            HttpRequestException => "连接失败，请检查网络或代理后重试。",
            UnauthorizedAccessException => "无法写入目录，请检查权限或更换下载目录。",
            IOException => "文件写入失败，请检查磁盘空间及文件是否被占用。",
            _ => "下载未完成，请重试；详细原因见运行日志。"
        };
    }
}
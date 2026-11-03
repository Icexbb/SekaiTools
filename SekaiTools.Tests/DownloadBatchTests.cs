using System.Collections.Concurrent;
using System.Net;
using SekaiDataFetch;

namespace SekaiTools.Tests;

public class DownloadBatchTests
{
    private static DownloadJob Job(int index, string? path = null)
    {
        return new DownloadJob(Guid.NewGuid(), $"Story {index}", $"https://download.invalid/{index}.json",
            path ?? Path.Combine(Path.GetTempPath(), $"SekaiTools-not-written-{Guid.NewGuid():N}.json"), "Source");
    }

    [Fact]
    public async Task LimitsConcurrencyToThreeAndKeepsBatchSnapshotIndependent()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var peak = 0;
        var calls = 0;
        var batch = new DownloadBatch(async (_, _, token) =>
        {
            var count = Interlocked.Increment(ref active);
            lock (gate)
            {
                peak = Math.Max(peak, count);
            }

            if (Interlocked.Increment(ref calls) == 3) entered.SetResult();
            await gate.Task.WaitAsync(token);
            Interlocked.Decrement(ref active);
        });
        var jobs = Enumerable.Range(0, 7).Select(index => Job(index)).ToList();
        var updates = new ConcurrentBag<DownloadUpdate>();
        var run = batch.RunAsync(jobs.ToArray(), ExistingFilePolicy.Replace, updates.Add);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        jobs.Add(Job(99));
        Assert.Equal(3, Volatile.Read(ref calls));
        gate.SetResult();
        var result = await run;
        Assert.Equal(3, peak);
        Assert.Equal(7, result.Succeeded);
        Assert.DoesNotContain(updates, update => update.Job.Id == jobs[^1].Id);
    }

    [Fact]
    public async Task StopMarksUnstartedTasksAndRetryCanFinishThem()
    {
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var jobs = Enumerable.Range(0, 8).Select(index => Job(index)).ToArray();
        var updates = new ConcurrentBag<DownloadUpdate>();
        var batch = new DownloadBatch(async (_, _, token) =>
        {
            if (Interlocked.Increment(ref calls) == 3) entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        var run = batch.RunAsync(jobs, ExistingFilePolicy.Skip, updates.Add, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        var result = await run;
        Assert.Equal(8, result.Stopped);
        Assert.Equal(3, calls);
        Assert.Equal(8, updates.Count(update => update.Status == DownloadStatus.Stopped));
        var retry = new DownloadBatch((_, _, _) => Task.CompletedTask);
        Assert.Equal(8, (await retry.RunAsync(jobs, ExistingFilePolicy.Skip, _ => { })).Succeeded);
    }

    [Fact]
    public async Task FailureDoesNotBlockOthersAndDuplicatePathsAreRejectedBeforeWriting()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SekaiTools-collision-{Guid.NewGuid():N}.json");
        var jobs = new[] { Job(0, path), Job(1, path), Job(2), Job(3) };
        var updates = new ConcurrentBag<DownloadUpdate>();
        var calls = new ConcurrentBag<Guid>();
        var batch = new DownloadBatch((job, _, _) =>
        {
            calls.Add(job.Id);
            return job.Id == jobs[2].Id ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask;
        });
        var result = await batch.RunAsync(jobs, ExistingFilePolicy.Replace, updates.Add);
        Assert.Equal(3, result.Failed);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, calls.Count);
        Assert.Contains(updates, update => update.Error != null && update.Error.Contains("文件名冲突"));
    }

    [Fact]
    public async Task ExistingFileSkipsRequestButExplicitReplaceRunsIt()
    {
        var path = Path.GetTempFileName();
        try
        {
            var calls = 0;
            var batch = new DownloadBatch((_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            });
            var jobs = new[] { Job(0, path) };
            Assert.Equal(1, (await batch.RunAsync(jobs, ExistingFilePolicy.Skip, _ => { })).Skipped);
            Assert.Equal(0, calls);
            Assert.Equal(1, (await batch.RunAsync(jobs, ExistingFilePolicy.Replace, _ => { })).Succeeded);
            Assert.Equal(1, calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SkipPolicyDoesNotReplaceFileCreatedWhileRequestWasInFlight()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SekaiTools-download-race-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "story.json");
        using var fetcher = new Fetcher(_ => new RaceHandler(path));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => fetcher.FetchToFile("https://download.invalid/story.json", path,
                overwrite: false));
            Assert.Equal("created elsewhere", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class RaceHandler(string path) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(path, "created elsewhere", cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
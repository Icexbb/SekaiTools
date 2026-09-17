using SekaiToolsMedia;
using Xunit;

namespace SekaiTools.Tests;

public class VideoSuppressionQueueTests
{
    private static VideoSuppressionJob Job(string name) => new(new(name, "", name + ".mp4", new()));

    private static Task Finished(VideoSuppressionJob job)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        job.ProgressChanged += p =>
        {
            if (p.State is VideoSuppressionState.Completed or VideoSuppressionState.Cancelled or VideoSuppressionState.Failed)
                done.TrySetResult();
        };
        return done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunsInOrderAndContinuesAfterFailure()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        using var queue = new VideoSuppressionQueue(async (options, _, _) =>
        {
            calls.Add(options.SourceVideo);
            if (options.SourceVideo == "first")
            {
                entered.SetResult();
                await release.Task;
                throw new IOException("test failure");
            }
        });
        var first = Job("first");
        var second = Job("second");
        var done = Finished(second);
        queue.Enqueue(first);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        queue.Enqueue(second);
        Assert.Equal(VideoSuppressionState.Idle, second.Progress.State);
        release.SetResult();
        await done;
        Assert.Equal(new[] { "first", "second" }, calls);
        Assert.Equal(VideoSuppressionState.Failed, first.Progress.State);
    }

    [Fact]
    public async Task CancelsRunningAndSkipsCancelledPendingJob()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        using var queue = new VideoSuppressionQueue(async (options, _, token) =>
        {
            calls.Add(options.SourceVideo);
            if (options.SourceVideo == "first")
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
        });
        var first = Job("first");
        var skipped = Job("skipped");
        var last = Job("last");
        var done = Finished(last);
        queue.Enqueue(first);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        queue.Enqueue(skipped);
        queue.Enqueue(last);
        skipped.Cancel();
        first.Cancel();
        await done;
        Assert.Equal(new[] { "first", "last" }, calls);
        Assert.Equal(VideoSuppressionState.Cancelled, first.Progress.State);
        Assert.Equal(VideoSuppressionState.Cancelled, skipped.Progress.State);
    }

    [Fact]
    public async Task DisposalCancelsPendingJobsAndRejectsNewEntries()
    {
        var queue = new VideoSuppressionQueue((_, _, token) => Task.Delay(Timeout.Infinite, token));
        var job = Job("first");
        queue.Enqueue(job);
        queue.Dispose();
        await queue.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(VideoSuppressionState.Cancelled, job.Progress.State);
        Assert.Throws<ObjectDisposedException>(() => queue.Enqueue(Job("late")));
    }

    [Fact]
    public async Task AsyncDisposalWaitsForCancellationWithoutBlockingCaller()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new VideoSuppressionQueue(async (_, _, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });

        queue.Enqueue(Job("running"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var disposeTask = queue.DisposeAsync(TimeSpan.FromSeconds(2));
        Assert.False(disposeTask.IsCompletedSuccessfully);
        await disposeTask;
        Assert.True(queue.Completion.IsCompleted);
    }
}

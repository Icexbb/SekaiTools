using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class BoundedMediaAnalysisTests
{
    [Fact]
    public async Task TimedOutNativeReadRetainsSlotUntilItActuallyExits()
    {
        var analysis = new BoundedMediaAnalysis();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = analysis.RunAsync(() =>
        {
            started.SetResult();
            release.Wait();
            return 1;
        }, TimeSpan.FromSeconds(1));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => first);
            var calls = 0;
            await Assert.ThrowsAsync<TimeoutException>(() =>
                analysis.RunAsync(() => Interlocked.Increment(ref calls), TimeSpan.FromMilliseconds(100)));
            Assert.Equal(0, calls);
        }
        finally
        {
            release.Set();
        }

        Assert.Equal(2, await analysis.RunAsync(() => 2, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CancelledWaitingReadNeverRunsAndFailuresReleaseSlot()
    {
        var analysis = new BoundedMediaAnalysis();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            analysis.RunAsync(() => Interlocked.Increment(ref calls), TimeSpan.FromSeconds(5), cancelled.Token));
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            analysis.RunAsync<int>(() => throw new InvalidDataException(), TimeSpan.FromSeconds(5)));
        Assert.Equal(3, await analysis.RunAsync(() => 3, TimeSpan.FromSeconds(5)));
    }
}
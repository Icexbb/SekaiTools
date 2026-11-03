using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class CoalescingProgressWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowOrFailedWriteDoesNotRetainOldSnapshotsOrLoseFinalState(bool failFirst)
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var written = new List<int>();
        var failures = new List<Exception>();
        var writer = new CoalescingProgressWriter((key, state) =>
        {
            written.Add(state.FrameIndex);
            if (state.FrameIndex == 1)
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                if (failFirst) throw new IOException();
            }
        }, failures.Add);
        writer.Enqueue("task", new ProcessingState { FrameIndex = 1 });
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
            writer.Enqueue("task", new ProcessingState { FrameIndex = 2 });
            writer.Enqueue("task", new ProcessingState { FrameIndex = 3 });
        }
        finally
        {
            release.Set();
        }

        await writer.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { 1, 3 }, written);
        Assert.Equal(failFirst ? 1 : 0, failures.Count);
        writer.Enqueue("task", new ProcessingState { FrameIndex = 4 });
        await writer.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { 1, 3, 4 }, written);
    }
}
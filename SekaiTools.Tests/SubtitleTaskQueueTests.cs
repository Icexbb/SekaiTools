using SekaiToolsGUI.Service;

namespace SekaiTools.Tests;

public sealed class SubtitleTaskQueueTests
{
    [Fact]
    public async Task ReorderedPendingTasksRunSeriallyAndHistoryIsNeverExecuted()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<string>();
        var queue = new SequentialTaskQueue<string>(async task =>
        {
            started.Add(task);
            if (task == "first") await gate.Task;
        });
        queue.PendingTasks.Add("first");
        queue.PendingTasks.Add("second");
        queue.PendingTasks.Add("third");
        queue.HistoryTasks.Add("history");
        var running = queue.RunAsync();
        Assert.Same(running, queue.RunAsync());
        Assert.Equal(new[] { "first" }, started);
        Assert.Equal(new[] { "first" }, queue.ProcessingTasks);
        queue.PendingTasks.Move(1, 0);
        queue.PendingTasks.Add("added while running");
        gate.SetResult();
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "first", "third", "second", "added while running" }, started);
        Assert.Equal(started, queue.FinishedTasks);
        Assert.Empty(queue.PendingTasks);
        Assert.Empty(queue.ProcessingTasks);
        Assert.Equal(new[] { "history" }, queue.HistoryTasks);
    }

    [Fact]
    public async Task FailureAndCancellationDoNotBlockFollowingTasks()
    {
        var started = new List<string>();
        var failed = new List<string>();
        var queue = new SequentialTaskQueue<string>(task =>
        {
            started.Add(task);
            return task switch
            {
                "failed" => Task.FromException(new InvalidOperationException()),
                "canceled" => Task.FromCanceled(new CancellationToken(true)),
                _ => Task.CompletedTask
            };
        });
        queue.TaskFailed += (task, _) => failed.Add(task);
        foreach (var task in new[] { "failed", "canceled", "finished" }) queue.PendingTasks.Add(task);
        await queue.RunAsync();
        Assert.Equal(new[] { "failed", "canceled", "finished" }, started);
        Assert.Equal(new[] { "failed", "canceled" }, failed);
        Assert.Equal(started, queue.FinishedTasks);
    }

    [Fact]
    public async Task StoppingWaitsForCurrentTaskAndLeavesPendingTasksUnstarted()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<string>();
        var queue = new SequentialTaskQueue<string>(async task => { started.Add(task); await gate.Task; });
        queue.PendingTasks.Add("current");
        queue.PendingTasks.Add("pending");
        var running = queue.RunAsync();
        var stopping = queue.StopAsync();
        Assert.False(stopping.IsCompleted);
        gate.SetResult();
        await Task.WhenAll(running, stopping).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "current" }, started);
        Assert.Equal(new[] { "pending" }, queue.PendingTasks);
        await queue.RunAsync();
        Assert.Equal(new[] { "current" }, started);
    }

    [Fact]
    public async Task IdleQueueCanStartAgainAfterNewTasksAreAdded()
    {
        var started = new List<string>();
        var queue = new SequentialTaskQueue<string>(task => { started.Add(task); return Task.CompletedTask; });
        queue.PendingTasks.Add("first");
        await queue.RunAsync();
        queue.PendingTasks.Add("next");
        await queue.RunAsync();
        Assert.Equal(new[] { "first", "next" }, started);
    }
}

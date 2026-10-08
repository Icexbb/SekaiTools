using System.Collections.ObjectModel;

namespace SekaiToolsGUI.Service;

/// <summary>A single-consumer queue. Access it on the owning UI context.</summary>
public sealed class SequentialTaskQueue<T>(Func<T, Task> execute) where T : class
{
    private TaskCompletionSource? _completion;
    private bool _stopping;

    public ObservableCollection<T> PendingTasks { get; } = [];
    public ObservableCollection<T> ProcessingTasks { get; } = [];
    public ObservableCollection<T> FinishedTasks { get; } = [];
    public ObservableCollection<T> HistoryTasks { get; } = [];
    public event Action<T, Exception>? TaskFailed;

    public Task RunAsync()
    {
        if (_completion != null) return _completion.Task;
        if (_stopping || PendingTasks.Count == 0) return Task.CompletedTask;

        // Reserve the consumer before executing any task or collection callbacks.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        _ = DrainAsync(completion);
        return completion.Task;
    }

    public Task StopAsync()
    {
        _stopping = true;
        return _completion?.Task ?? Task.CompletedTask;
    }

    private async Task DrainAsync(TaskCompletionSource completion)
    {
        try
        {
            while (!_stopping && PendingTasks.Count > 0)
            {
                var task = PendingTasks[0];
                PendingTasks.RemoveAt(0);
                ProcessingTasks.Add(task);
                try
                {
                    await execute(task);
                }
                catch (Exception exception)
                {
                    TaskFailed?.Invoke(task, exception);
                }
                finally
                {
                    ProcessingTasks.Remove(task);
                    FinishedTasks.Add(task);
                }
            }
        }
        catch (Exception exception)
        {
            _completion = null;
            completion.TrySetException(exception);
            return;
        }
        finally
        {
            _completion = null;
        }
        completion.TrySetResult();
    }
}

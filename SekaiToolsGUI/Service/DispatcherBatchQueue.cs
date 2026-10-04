using System.Windows.Threading;

namespace SekaiToolsGUI.Service;

internal sealed class DispatcherBatchQueue(Dispatcher dispatcher, Func<IDisposable> beginBatch)
{
    private readonly Queue<Action> _pending = new();
    private readonly SemaphoreSlim _slots = new(128, 128);
    private bool _scheduled;
    private Exception? _error;

    internal void Enqueue(Action action)
    {
        if (dispatcher.CheckAccess()) { action(); return; }
        _slots.Wait();
        lock (_pending)
        {
            _pending.Enqueue(action);
            if (_scheduled) return;
            _scheduled = true;
            dispatcher.BeginInvoke(Drain, DispatcherPriority.Background);
        }
    }

    internal async Task FlushAsync()
    {
        if (dispatcher.CheckAccess())
        {
            while (true)
            {
                lock (_pending) { if (_pending.Count == 0) break; }
                Drain();
            }
        }
        else
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(() => done.TrySetResult());
            await done.Task.ConfigureAwait(false);
        }
        if (Interlocked.Exchange(ref _error, null) is { } error)
            throw new InvalidOperationException("批量更新识别结果失败", error);
    }

    private void Drain()
    {
        var batch = new List<Action>(32);
        lock (_pending)
            while (batch.Count < 32 && _pending.TryDequeue(out var action)) batch.Add(action);
        try
        {
            using var scope = beginBatch();
            foreach (var action in batch)
                try { action(); }
                catch (Exception error) { Interlocked.CompareExchange(ref _error, error, null); }
        }
        finally
        {
            if (batch.Count > 0) _slots.Release(batch.Count);
            lock (_pending)
            {
                _scheduled = _pending.Count != 0;
                if (_scheduled) dispatcher.BeginInvoke(Drain, DispatcherPriority.Background);
            }
        }
    }
}

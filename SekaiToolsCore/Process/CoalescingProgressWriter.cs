namespace SekaiToolsCore.Process;

/// <summary>One active write and one latest pending snapshot, owned by one processor.</summary>
internal sealed class CoalescingProgressWriter(Action<string, ProcessingState> save, Action<Exception> report)
{
    private readonly object _gate = new();
    private (string Key, ProcessingState State)? _pending;
    private bool _running;
    private Task _worker = Task.CompletedTask;

    public Task Completion
    {
        get
        {
            lock (_gate)
            {
                return _worker;
            }
        }
    }

    public void Enqueue(string key, ProcessingState state)
    {
        lock (_gate)
        {
            _pending = (key, state);
            if (_running) return;
            _running = true;
            _worker = Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            (string Key, ProcessingState State) next;
            lock (_gate)
            {
                if (_pending is not { } pending)
                {
                    _running = false;
                    return;
                }

                next = pending;
                _pending = null;
            }

            try
            {
                save(next.Key, next.State);
            }
            catch (Exception exception)
            {
                report(exception);
            }
        }
    }
}
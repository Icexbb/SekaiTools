namespace SekaiToolsMedia;

/// <summary>Bounds native readers; cancellation stops waiting without releasing an active native reader's slot.</summary>
public sealed class BoundedMediaAnalysis
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<T> RunAsync<T>(Func<T> read, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
            var work = Task.Run(() =>
            {
                try { return read(); }
                finally { _gate.Release(); }
            });
            return await work.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("视频分析超时，请重试或选择本地视频；上次读取结束后会继续分析。"); }
    }
}

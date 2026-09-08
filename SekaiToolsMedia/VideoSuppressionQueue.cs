using System.Threading.Channels;

namespace SekaiToolsMedia;

/// <summary>Serial, in-memory suppression queue. Each entry owns its options and cancellation.</summary>
public sealed class VideoSuppressionQueue : IDisposable
{
    private readonly Channel<VideoSuppressionJob> _jobs = Channel.CreateUnbounded<VideoSuppressionJob>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<VideoSuppressionOptions, Action<VideoSuppressionProgress>, CancellationToken, Task> _run;

    public VideoSuppressionQueue(Func<VideoSuppressionOptions, Action<VideoSuppressionProgress>, CancellationToken, Task> run)
    {
        _run = run;
        Completion = Task.Run(ProcessAsync);
    }

    public Task Completion { get; }

    public void Enqueue(VideoSuppressionJob job)
    {
        if (!job.TryEnqueue()) throw new InvalidOperationException("任务已加入队列");
        if (!_jobs.Writer.TryWrite(job)) throw new ObjectDisposedException(nameof(VideoSuppressionQueue));
    }

    private async Task ProcessAsync()
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync())
        {
            using var source = CancellationTokenSource.CreateLinkedTokenSource(job.Token, _shutdown.Token);
            try
            {
                source.Token.ThrowIfCancellationRequested();
                job.Report(new(0, job.Options.SourceFrameCount, 0, VideoSuppressionState.Preparing, "正在准备压制…"));
                await _run(job.Options, job.Report, source.Token).ConfigureAwait(false);
                job.Finish(VideoSuppressionState.Completed);
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
                job.Finish(VideoSuppressionState.Cancelled);
            }
            catch (Exception ex)
            {
                job.Finish(VideoSuppressionState.Failed, $"压制失败：{ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _jobs.Writer.TryComplete();
        _shutdown.Cancel();
    }
}

public sealed class VideoSuppressionJob(VideoSuppressionOptions options)
{
    private readonly CancellationTokenSource _cancellation = new();
    private int _enqueued;
    public VideoSuppressionOptions Options { get; } = options;
    public VideoSuppressionProgress Progress { get; private set; } = new(
        0, options.SourceFrameCount, 0, VideoSuppressionState.Idle, "等待中");
    public event Action<VideoSuppressionProgress>? ProgressChanged;
    internal CancellationToken Token => _cancellation.Token;
    internal bool TryEnqueue() => Interlocked.Exchange(ref _enqueued, 1) == 0;
    public void Cancel() => _cancellation.Cancel();
    internal void Report(VideoSuppressionProgress progress)
    {
        Progress = progress;
        ProgressChanged?.Invoke(progress);
    }
    internal void Finish(VideoSuppressionState state, string? log = null)
    {
        var finalLog = string.IsNullOrWhiteSpace(log)
            ? Progress.Log
            : $"{Progress.Log}\n{log}";
        Report(Progress with { State = state, Log = finalLog.Trim() });
    }
}

namespace SekaiToolsCore.Process.Performance;

public enum ProcessingStage
{
    Decode,
    Preprocess,
    Match
}

public sealed record ProcessingPerformanceSnapshot(
    long FrameCount,
    TimeSpan DecodeTime,
    TimeSpan PreprocessTime,
    TimeSpan MatchTime)
{
    public TimeSpan MeasuredTime => DecodeTime + PreprocessTime + MatchTime;

    public double AverageMillisecondsPerFrame => FrameCount > 0
        ? MeasuredTime.TotalMilliseconds / FrameCount
        : 0;

    public override string ToString()
    {
        return $"帧数={FrameCount}, 解码={DecodeTime.TotalMilliseconds:F1}ms, " +
               $"预处理={PreprocessTime.TotalMilliseconds:F1}ms, " +
               $"匹配={MatchTime.TotalMilliseconds:F1}ms, " +
               $"平均={AverageMillisecondsPerFrame:F3}ms/帧";
    }
}

public sealed class ProcessingPerformanceMetrics
{
    private long _decodeTicks;
    private long _frameCount;
    private long _matchTicks;
    private long _preprocessTicks;

    internal void Record(ProcessingStage stage, TimeSpan elapsed)
    {
        var ticks = elapsed.Ticks;
        switch (stage)
        {
            case ProcessingStage.Decode:
                Interlocked.Add(ref _decodeTicks, ticks);
                break;
            case ProcessingStage.Preprocess:
                Interlocked.Add(ref _preprocessTicks, ticks);
                break;
            case ProcessingStage.Match:
                Interlocked.Add(ref _matchTicks, ticks);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
        }
    }

    internal void RecordFrame()
    {
        Interlocked.Increment(ref _frameCount);
    }

    internal void Reset()
    {
        Interlocked.Exchange(ref _frameCount, 0);
        Interlocked.Exchange(ref _decodeTicks, 0);
        Interlocked.Exchange(ref _preprocessTicks, 0);
        Interlocked.Exchange(ref _matchTicks, 0);
    }

    public ProcessingPerformanceSnapshot Snapshot()
    {
        return new ProcessingPerformanceSnapshot(
            Interlocked.Read(ref _frameCount),
            TimeSpan.FromTicks(Interlocked.Read(ref _decodeTicks)),
            TimeSpan.FromTicks(Interlocked.Read(ref _preprocessTicks)),
            TimeSpan.FromTicks(Interlocked.Read(ref _matchTicks)));
    }
}

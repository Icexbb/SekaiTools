using System.Diagnostics;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiToolsCore.Process.Performance;

public enum TemplateMatchingStage
{
    Resize,
    Correlation,
    ScoreCleanup,
    Extremum
}

public enum TemplateMatchingState
{
    Searching,
    Acquiring,
    Tracking,
    Recovering,
    Lookahead
}

public sealed record TemplateMatchingStateSnapshot(
    string Usage,
    string State,
    long Requests,
    long CacheHits,
    long CorrelationCalls,
    TimeSpan Elapsed);

public sealed record TemplateMatchingUsageSnapshot(string Usage, long Requests, long CacheHits, TimeSpan Elapsed);

public sealed record TemplateMatchingSnapshot(
    IReadOnlyList<TemplateMatchingUsageSnapshot> Usages,
    long CorrelationCalls,
    TimeSpan ResizeTime,
    TimeSpan CorrelationTime,
    TimeSpan ScoreCleanupTime,
    TimeSpan ExtremumTime)
{
    public IReadOnlyList<TemplateMatchingStateSnapshot> States { get; init; } = [];
    public long Requests => Usages.Sum(u => u.Requests);
    public long CacheHits => Usages.Sum(u => u.CacheHits);
    public double CacheHitRate => Requests == 0 ? 0 : (double)CacheHits / Requests;

    public override string ToString()
    {
        return $"请求={Requests}, 缓存命中={CacheHits} ({CacheHitRate:P1}), 相关计算={CorrelationCalls}, " +
               $"ROI缩放={ResizeTime.TotalMilliseconds:F1}ms, 相关计算={CorrelationTime.TotalMilliseconds:F1}ms, " +
               $"分数清理={ScoreCleanupTime.TotalMilliseconds:F1}ms, 极值查找={ExtremumTime.TotalMilliseconds:F1}ms; " +
               string.Join("; ", Usages.Where(u => u.Requests > 0)
                   .Select(u => $"{u.Usage}: {u.Elapsed.TotalMilliseconds:F1}ms/{u.Requests}次")) +
               "; " + string.Join("; ", States.Select(s =>
                   $"{s.Usage}/{s.State}: {s.Elapsed.TotalMilliseconds:F1}ms/{s.Requests}次/{s.CorrelationCalls}相关计算"));
    }
}

/// <summary>任务级统计。算子计时是匹配耗时的子集，不应累加到总耗时。</summary>
public sealed class TemplateMatchingMetrics
{
    private const int StateCount = 5;
    private readonly long[] _hits = new long[8];
    private readonly long[] _requests = new long[8];
    private readonly long[] _stageTicks = new long[4];
    private readonly long[] _stateCorrelations = new long[8 * StateCount];
    private readonly long[] _stateHits = new long[8 * StateCount];
    private readonly long[] _stateRequests = new long[8 * StateCount];
    private readonly long[] _stateTicks = new long[8 * StateCount];
    private readonly long[] _usageTicks = new long[8];
    private long _correlationCalls;
    public bool Enabled { get; set; }
    internal TemplateMatchingState CurrentState { get; private set; }
    internal TemplateMatchCachePool.MatchUsage CurrentUsage { get; private set; }

    // Scopes are confined to the sequential recognition consumer, never the decoder/preloader.
    internal MeasurementScope Enter(TemplateMatchingState state, TemplateMatchCachePool.MatchUsage? usage = null)
    {
        if (!Enabled) return default;
        var scope = new MeasurementScope(this, CurrentState, CurrentUsage);
        CurrentState = state;
        if (usage.HasValue) CurrentUsage = usage.Value;
        return scope;
    }

    internal void RecordRequest(TemplateMatchCachePool.MatchUsage usage, bool hit, long started)
    {
        if (!Enabled) return;
        var ticks = Stopwatch.GetElapsedTime(started).Ticks;
        Interlocked.Increment(ref _requests[(int)usage]);
        if (hit) Interlocked.Increment(ref _hits[(int)usage]);
        Interlocked.Add(ref _usageTicks[(int)usage], ticks);
        var index = (int)usage * StateCount + (int)CurrentState;
        Interlocked.Increment(ref _stateRequests[index]);
        if (hit) Interlocked.Increment(ref _stateHits[index]);
        Interlocked.Add(ref _stateTicks[index], ticks);
    }

    internal void Record(TemplateMatchingStage stage, long started)
    {
        if (!Enabled) return;
        Interlocked.Add(ref _stageTicks[(int)stage], Stopwatch.GetElapsedTime(started).Ticks);
        if (stage == TemplateMatchingStage.Correlation)
        {
            Interlocked.Increment(ref _correlationCalls);
            Interlocked.Increment(ref _stateCorrelations[(int)CurrentUsage * StateCount + (int)CurrentState]);
        }
    }

    internal void Reset()
    {
        foreach (var values in new[]
                 {
                     _requests, _hits, _usageTicks, _stageTicks,
                     _stateRequests, _stateHits, _stateTicks, _stateCorrelations
                 })
            for (var i = 0; i < values.Length; i++)
                Interlocked.Exchange(ref values[i], 0);
        Interlocked.Exchange(ref _correlationCalls, 0);
        CurrentState = TemplateMatchingState.Searching;
        CurrentUsage = default;
    }

    public TemplateMatchingSnapshot Snapshot()
    {
        return new TemplateMatchingSnapshot(
            Enumerable.Range(0, _requests.Length).Select(i => new TemplateMatchingUsageSnapshot(
                ((TemplateMatchCachePool.MatchUsage)i).ToString(), Interlocked.Read(ref _requests[i]),
                Interlocked.Read(ref _hits[i]), TimeSpan.FromTicks(Interlocked.Read(ref _usageTicks[i])))).ToArray(),
            Interlocked.Read(ref _correlationCalls), Read(0), Read(1), Read(2), Read(3))
        {
            States = Enumerable.Range(0, _stateRequests.Length)
                .Where(i => Interlocked.Read(ref _stateRequests[i]) > 0 ||
                            Interlocked.Read(ref _stateCorrelations[i]) > 0)
                .Select(i => new TemplateMatchingStateSnapshot(
                    ((TemplateMatchCachePool.MatchUsage)(i / StateCount)).ToString(),
                    ((TemplateMatchingState)(i % StateCount)).ToString(),
                    Interlocked.Read(ref _stateRequests[i]), Interlocked.Read(ref _stateHits[i]),
                    Interlocked.Read(ref _stateCorrelations[i]),
                    TimeSpan.FromTicks(Interlocked.Read(ref _stateTicks[i]))))
                .ToArray()
        };
    }

    private TimeSpan Read(int index)
    {
        return TimeSpan.FromTicks(Interlocked.Read(ref _stageTicks[index]));
    }

    internal readonly struct MeasurementScope(
        TemplateMatchingMetrics owner,
        TemplateMatchingState previousState,
        TemplateMatchCachePool.MatchUsage previousUsage) : IDisposable
    {
        public void Dispose()
        {
            if (owner == null) return;
            owner.CurrentState = previousState;
            owner.CurrentUsage = previousUsage;
        }
    }
}
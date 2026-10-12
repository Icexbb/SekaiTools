using System.Diagnostics;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiToolsCore.Process.Performance;

public enum TemplateMatchingStage { Resize, Correlation, ScoreCleanup, Extremum }

public sealed record TemplateMatchingUsageSnapshot(string Usage, long Requests, long CacheHits, TimeSpan Elapsed);

public sealed record TemplateMatchingSnapshot(
    IReadOnlyList<TemplateMatchingUsageSnapshot> Usages,
    long CorrelationCalls, TimeSpan ResizeTime, TimeSpan CorrelationTime,
    TimeSpan ScoreCleanupTime, TimeSpan ExtremumTime)
{
    public long Requests => Usages.Sum(u => u.Requests);
    public long CacheHits => Usages.Sum(u => u.CacheHits);
    public double CacheHitRate => Requests == 0 ? 0 : (double)CacheHits / Requests;
    public override string ToString() =>
        $"请求={Requests}, 缓存命中={CacheHits} ({CacheHitRate:P1}), 相关计算={CorrelationCalls}, " +
        $"ROI缩放={ResizeTime.TotalMilliseconds:F1}ms, 相关计算={CorrelationTime.TotalMilliseconds:F1}ms, " +
        $"分数清理={ScoreCleanupTime.TotalMilliseconds:F1}ms, 极值查找={ExtremumTime.TotalMilliseconds:F1}ms; " +
        string.Join("; ", Usages.Where(u => u.Requests > 0)
            .Select(u => $"{u.Usage}: {u.Elapsed.TotalMilliseconds:F1}ms/{u.Requests}次"));
}

/// <summary>任务级统计。算子计时是匹配耗时的子集，不应累加到总耗时。</summary>
public sealed class TemplateMatchingMetrics
{
    public bool Enabled { get; set; }
    private readonly long[] _requests = new long[8];
    private readonly long[] _hits = new long[8];
    private readonly long[] _usageTicks = new long[8];
    private readonly long[] _stageTicks = new long[4];
    private long _correlationCalls;

    internal void RecordRequest(TemplateMatchCachePool.MatchUsage usage, bool hit, long started)
    {
        Interlocked.Increment(ref _requests[(int)usage]);
        if (hit) Interlocked.Increment(ref _hits[(int)usage]);
        Interlocked.Add(ref _usageTicks[(int)usage], Stopwatch.GetElapsedTime(started).Ticks);
    }

    internal void Record(TemplateMatchingStage stage, long started)
    {
        Interlocked.Add(ref _stageTicks[(int)stage], Stopwatch.GetElapsedTime(started).Ticks);
        if (stage == TemplateMatchingStage.Correlation) Interlocked.Increment(ref _correlationCalls);
    }

    internal void Reset()
    {
        foreach (var values in new[] { _requests, _hits, _usageTicks, _stageTicks })
            for (var i = 0; i < values.Length; i++) Interlocked.Exchange(ref values[i], 0);
        Interlocked.Exchange(ref _correlationCalls, 0);
    }

    public TemplateMatchingSnapshot Snapshot() => new(
        Enumerable.Range(0, _requests.Length).Select(i => new TemplateMatchingUsageSnapshot(
            ((TemplateMatchCachePool.MatchUsage)i).ToString(), Interlocked.Read(ref _requests[i]),
            Interlocked.Read(ref _hits[i]), TimeSpan.FromTicks(Interlocked.Read(ref _usageTicks[i])))).ToArray(),
        Interlocked.Read(ref _correlationCalls), Read(0), Read(1), Read(2), Read(3));

    private TimeSpan Read(int index) => TimeSpan.FromTicks(Interlocked.Read(ref _stageTicks[index]));
}

using SekaiToolsCore.Process;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Match.TemplateMatcher;

public abstract class MatcherStateMachine<T> where T : BaseFrameSet
{
    protected const double FallbackRatio = 0.7;
    protected const double AbsMinThreshold = 0.40;
    protected const double TrackingThresholdRatio = 0.94;

    private int _consecutiveFailures;
    private int _firstUnfinishedIndex;
    private int _lastFailedIndex = -1;

    protected MatcherStateMachine(List<T> frameSets, int fallbackTriggerFrames)
    {
        Set = frameSets;
        FallbackTriggerFrames = fallbackTriggerFrames;
    }

    public List<T> Set { get; }
    public List<MatcherDiagnostic> Diagnostics { get; } = [];
    protected int FallbackTriggerFrames { get; }
    protected bool IsUsingFallback { get; private set; }

    public bool Finished => Set.Count == 0 || Set.TrueForAll(d => d.Finished);

    protected int? LastFailureFrame { get; private set; }

    protected double EffectiveThreshold(double baseThreshold, bool isTracking = false)
    {
        var threshold = IsUsingFallback
            ? Math.Max(baseThreshold * FallbackRatio, AbsMinThreshold)
            : baseThreshold;
        return isTracking
            ? Math.Max(threshold * TrackingThresholdRatio, AbsMinThreshold)
            : threshold;
    }

    protected void ResetForNewTarget(int index)
    {
        if (index != _lastFailedIndex)
        {
            _lastFailedIndex = index;
            _consecutiveFailures = 0;
            LastFailureFrame = null;
            IsUsingFallback = false;
        }
    }

    protected bool TryEnterFallback(int frameIndex)
    {
        _consecutiveFailures += LastFailureFrame is { } previous ? Math.Max(0, frameIndex - previous) : 0;
        LastFailureFrame = frameIndex;
        if (_consecutiveFailures >= FallbackTriggerFrames && !IsUsingFallback)
        {
            IsUsingFallback = true;
            _consecutiveFailures = 0;
            LastFailureFrame = null;
            return true;
        }

        IsUsingFallback = false;
        return false;
    }

    protected void MarkSucceeded()
    {
        _consecutiveFailures = 0;
        LastFailureFrame = null;
        IsUsingFallback = false;
    }

    protected void MarkDropped(int index)
    {
        Set[index].Finished = true;
        _consecutiveFailures = 0;
        LastFailureFrame = null;
        IsUsingFallback = false;
    }

    protected void MarkMissing(int index, int frameIndex, string reason)
    {
        Set[index].Finished = true;
        Diagnostics.Add(new MatcherDiagnostic(typeof(T).Name, index, frameIndex, reason));
        _consecutiveFailures = 0;
        LastFailureFrame = null;
        IsUsingFallback = false;
    }

    protected int NextUnfinishedIndex()
    {
        while (_firstUnfinishedIndex < Set.Count && Set[_firstUnfinishedIndex].Finished)
            _firstUnfinishedIndex++;
        return _firstUnfinishedIndex < Set.Count ? _firstUnfinishedIndex : -1;
    }

    protected void ResetIndexTracking()
    {
        _firstUnfinishedIndex = 0;
    }

    protected (int consecutiveFailures, int lastFailedIndex, bool useFallbackThreshold) SaveFallbackState()
    {
        return (_consecutiveFailures, _lastFailedIndex, IsUsingFallback);
    }

    protected void RestoreFallbackState(int consecutiveFailures, int lastFailedIndex, bool useFallbackThreshold,
        int? lastFailureFrame = null)
    {
        _consecutiveFailures = consecutiveFailures;
        LastFailureFrame = lastFailureFrame;
        _lastFailedIndex = lastFailedIndex;
        IsUsingFallback = useFallbackThreshold;
        _firstUnfinishedIndex = 0;
    }

    protected List<MatcherDiagnosticDto> SaveDiagnostics()
    {
        return Diagnostics.Select(d => new MatcherDiagnosticDto
        {
            Matcher = d.Matcher,
            TargetIndex = d.TargetIndex,
            FrameIndex = d.FrameIndex,
            Reason = d.Reason
        }).ToList();
    }

    protected void RestoreDiagnostics(IEnumerable<MatcherDiagnosticDto> diagnostics)
    {
        Diagnostics.Clear();
        Diagnostics.AddRange(diagnostics.Select(d =>
            new MatcherDiagnostic(d.Matcher, d.TargetIndex, d.FrameIndex, d.Reason)));
    }
}
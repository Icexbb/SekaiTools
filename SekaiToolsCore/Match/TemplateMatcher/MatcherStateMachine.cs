using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Match.TemplateMatcher;

public abstract class MatcherStateMachine<T> where T : BaseFrameSet
{
    protected const double FallbackRatio = 0.7;
    protected const double AbsMinThreshold = 0.40;

    private int _consecutiveFailures;
    private int _lastFailedIndex = -1;
    private bool _useFallbackThreshold;
    private int _firstUnfinishedIndex;

    protected MatcherStateMachine(List<T> frameSets, int fallbackTriggerFrames)
    {
        Set = frameSets;
        FallbackTriggerFrames = fallbackTriggerFrames;
    }

    public List<T> Set { get; }
    protected int FallbackTriggerFrames { get; }

    public bool Finished => Set.Count == 0 || Set.TrueForAll(d => d.Finished);

    protected double EffectiveThreshold(double baseThreshold) =>
        _useFallbackThreshold
            ? Math.Max(baseThreshold * FallbackRatio, AbsMinThreshold)
            : baseThreshold;

    protected void ResetForNewTarget(int index)
    {
        if (index != _lastFailedIndex)
        {
            _lastFailedIndex = index;
            _consecutiveFailures = 0;
            _useFallbackThreshold = false;
        }
    }

    protected bool TryEnterFallback()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= FallbackTriggerFrames && !_useFallbackThreshold)
        {
            _useFallbackThreshold = true;
            _consecutiveFailures = 0;
            return true;
        }

        _useFallbackThreshold = false;
        return false;
    }

    protected void MarkSucceeded()
    {
        _consecutiveFailures = 0;
        _useFallbackThreshold = false;
    }

    protected void MarkDropped(int index)
    {
        Set[index].Finished = true;
        _consecutiveFailures = 0;
        _useFallbackThreshold = false;
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

    protected (int consecutiveFailures, int lastFailedIndex, bool useFallbackThreshold) SaveFallbackState() =>
        (_consecutiveFailures, _lastFailedIndex, _useFallbackThreshold);

    protected void RestoreFallbackState(int consecutiveFailures, int lastFailedIndex, bool useFallbackThreshold)
    {
        _consecutiveFailures = consecutiveFailures;
        _lastFailedIndex = lastFailedIndex;
        _useFallbackThreshold = useFallbackThreshold;
        _firstUnfinishedIndex = 0;
    }
}

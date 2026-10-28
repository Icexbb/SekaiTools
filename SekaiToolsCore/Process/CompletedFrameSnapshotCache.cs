namespace SekaiToolsCore.Process;

/// <summary>Only the processing-time checkpoint path reuses completed immutable DTOs.</summary>
internal sealed class CompletedFrameSnapshotCache<TFrame, TSnapshot> where TFrame : class
{
    private readonly Dictionary<TFrame, TSnapshot> _completed = new(ReferenceEqualityComparer.Instance);
    public TSnapshot Capture(TFrame frame, bool finished, bool checkpoint, Func<TFrame, TSnapshot> create)
    {
        if (!checkpoint || !finished) return create(frame);
        if (_completed.TryGetValue(frame, out var snapshot)) return snapshot;
        snapshot = create(frame);
        _completed.Add(frame, snapshot);
        return snapshot;
    }
    public void Clear() => _completed.Clear();
}

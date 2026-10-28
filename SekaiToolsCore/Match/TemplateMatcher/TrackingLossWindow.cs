namespace SekaiToolsCore.Match.TemplateMatcher;

/// <summary>Confirms disappearance without recording unobserved positions.</summary>
internal sealed class TrackingLossWindow(double fps)
{
    private readonly int _limit = Math.Max(1, (int)Math.Ceiling(fps * 0.05));
    public int? FirstMissingFrame { get; private set; }
    public bool ShouldWait(int frame)
    {
        FirstMissingFrame ??= frame;
        return frame - FirstMissingFrame.Value < _limit;
    }
    public void Clear() => FirstMissingFrame = null;
    public void Restore(int? frame) => FirstMissingFrame = frame;
}

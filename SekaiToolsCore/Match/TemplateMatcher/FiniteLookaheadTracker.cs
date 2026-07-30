namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class FiniteLookaheadTracker
{
    private int _firstFailureFrame = -1;
    private int _targetIndex = -1;

    public bool ShouldProbe(int targetIndex, int frameIndex, int triggerFrames)
    {
        if (_targetIndex != targetIndex)
        {
            _targetIndex = targetIndex;
            _firstFailureFrame = frameIndex;
            return false;
        }

        return _firstFailureFrame >= 0 && frameIndex - _firstFailureFrame >= triggerFrames;
    }

    public void Postpone(int frameIndex)
    {
        _firstFailureFrame = frameIndex;
    }

    public void Reset()
    {
        _targetIndex = -1;
        _firstFailureFrame = -1;
    }
}

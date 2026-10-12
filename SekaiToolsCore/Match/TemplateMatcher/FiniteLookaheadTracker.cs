using SekaiToolsCore.Process;

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

    public LookaheadStateDto CaptureState() => new(_firstFailureFrame, _targetIndex);

    public void RestoreState(LookaheadStateDto? state)
    {
        _firstFailureFrame = state?.FirstFailureFrame ?? -1;
        _targetIndex = state?.TargetIndex ?? -1;
    }

    public void Reset()
    {
        _targetIndex = -1;
        _firstFailureFrame = -1;
    }
}
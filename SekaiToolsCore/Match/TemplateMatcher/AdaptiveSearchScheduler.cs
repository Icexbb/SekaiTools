using SekaiToolsCore.Process;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class AdaptiveSearchScheduler(int searchInterval = 2) : IDisposable
{
    private int _lastSampleFrameIndex = -1;
    private int _previousSkippedFrameIndex = -1;

    public void Dispose()
    {
        ClearPrevious();
    }

    public bool ShouldSample(int frameIndex)
    {
        return _lastSampleFrameIndex < 0 || frameIndex - _lastSampleFrameIndex >= searchInterval;
    }

    public void RememberSkipped(int frameIndex)
    {
        _previousSkippedFrameIndex = frameIndex;
    }

    public bool TryGetPrevious(FrameMatchContext? previousFrame, int previousFrameIndex,
        out FrameMatchContext? frame, out int frameIndex)
    {
        if (previousFrame == null || previousFrameIndex != _previousSkippedFrameIndex)
        {
            frame = null;
            frameIndex = -1;
            return false;
        }

        frame = previousFrame;
        frameIndex = _previousSkippedFrameIndex;
        return true;
    }

    public void CompleteSample(int frameIndex)
    {
        _lastSampleFrameIndex = frameIndex;
        ClearPrevious();
    }

    public AdaptiveSearchStateDto CaptureState() => new(_lastSampleFrameIndex, _previousSkippedFrameIndex);

    public void RestoreState(AdaptiveSearchStateDto? state)
    {
        _lastSampleFrameIndex = state?.LastSampleFrameIndex ?? -1;
        _previousSkippedFrameIndex = state?.PreviousSkippedFrameIndex ?? -1;
    }

    public void Reset()
    {
        _lastSampleFrameIndex = -1;
        ClearPrevious();
    }

    private void ClearPrevious()
    {
        _previousSkippedFrameIndex = -1;
    }
}
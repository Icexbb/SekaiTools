using SekaiToolsCore.Process;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class FiniteLookaheadTracker
{
    private bool _changePending;
    private byte[]? _evidence;
    private int _failedProbes;
    private int _firstFailureFrame = -1;
    private int _lastProbeFrame = -1;
    private int _targetIndex = -1;

    public bool ShouldProbe(int targetIndex, int frameIndex, int triggerFrames, byte[]? evidence = null)
    {
        if (_targetIndex != targetIndex)
        {
            Reset();
            _targetIndex = targetIndex;
            _evidence = evidence;
            _firstFailureFrame = frameIndex;
            return false;
        }

        if (evidence != null)
        {
            if (VisualChangeEvidence.HasChanged(_evidence, evidence))
            {
                _changePending = true;
                _failedProbes = 0;
                _evidence = evidence;
            }

            _evidence ??= evidence;
        }

        if (_lastProbeFrame == frameIndex) return false;
        var elapsed = (long)frameIndex - _firstFailureFrame;
        var interval = (long)triggerFrames * (1 << Math.Clamp(_failedProbes - 1, 0, 2));
        var due = _firstFailureFrame >= 0 && (elapsed >= interval ||
                                              (_changePending && elapsed >= Math.Max(2, triggerFrames / 6)));
        if (due) _lastProbeFrame = frameIndex;
        return due;
    }

    public void Postpone(int frameIndex)
    {
        _firstFailureFrame = frameIndex;
        _failedProbes = Math.Min(3, _failedProbes + 1);
        _changePending = false;
    }

    public LookaheadStateDto CaptureState()
    {
        return new LookaheadStateDto(_firstFailureFrame, _targetIndex)
        {
            FailedProbes = _failedProbes,
            LastProbeFrame = _lastProbeFrame,
            Evidence = _evidence?.ToArray(),
            ChangePending = _changePending
        };
    }

    public void RestoreState(LookaheadStateDto? state)
    {
        if (state?.FailedProbes is < 0 or > 3 || state?.Evidence is { Length: not 128 })
            throw new InvalidDataException("Invalid lookahead scheduling state");
        _failedProbes = state?.FailedProbes ?? 0;
        _lastProbeFrame = state?.LastProbeFrame ?? -1;
        _evidence = state?.Evidence?.ToArray();
        _changePending = state?.ChangePending ?? false;
        _firstFailureFrame = state?.FirstFailureFrame ?? -1;
        _targetIndex = state?.TargetIndex ?? -1;
    }

    public void Reset()
    {
        _targetIndex = -1;
        _firstFailureFrame = -1;
        _failedProbes = 0;
        _lastProbeFrame = -1;
        _evidence = null;
        _changePending = false;
    }
}
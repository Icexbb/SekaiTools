using System.Drawing;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Match.TemplateMatcher;

/// <summary>Holds uncertain tracking frames without extending an exported dialogue.</summary>
internal sealed class DialogTrackingGrace(double fps)
{
    private readonly List<FrameResultDto> _pending = [];
    private readonly int _maximumFrames = Math.Max(1, (int)Math.Ceiling(fps * 0.05));

    public bool Hold(int index, Point point)
    {
        if (_pending.Count > 0 && index - _pending[0].Index >= _maximumFrames)
            return false;
        _pending.Add(new FrameResultDto(index, point.X, point.Y));
        return true;
    }

    public void Commit(DialogBaseFrameSet target)
    {
        foreach (var frame in _pending)
            target.Add(frame.Index, new Point(frame.X, frame.Y));
        Clear();
    }

    public void Clear() => _pending.Clear();
    public List<FrameResultDto> CaptureState() => [.. _pending];
    public void RestoreState(IEnumerable<FrameResultDto> frames)
    {
        Clear();
        _pending.AddRange(frames);
    }
}

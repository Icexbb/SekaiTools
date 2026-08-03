using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public class MarkerFrameResult(int index, FrameRate fps, Point point) : ProcessFrame(index, fps)
{
    public Point Point => point;
}

public class MarkerBaseFrameSet(MarkerStoryEvent data, FrameRate fps) : BaseFrameSet
{
    private List<MarkerFrameResult>? _timingSourceFrames;
    public MarkerStoryEvent Data { get; } = data;
    public FrameRate Fps { get; } = fps;
    public List<MarkerFrameResult> Frames { get; } = [];

    public void Add(int index, Point point)
    {
        Frames.Add(new MarkerFrameResult(index + FrameIndexOffset, Fps, point));
    }

    public void SetFrameRange(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "结束帧不能早于起始帧");
        if (Frames.Count == 0)
            return;

        _timingSourceFrames ??= Frames.OrderBy(frame => frame.Index).ToList();
        var source = _timingSourceFrames;
        var rebuilt = new List<MarkerFrameResult>(end - start + 1);
        var sourceIndex = 0;
        for (var frameIndex = start; frameIndex <= end; frameIndex++)
        {
            while (sourceIndex + 1 < source.Count &&
                   Math.Abs(source[sourceIndex + 1].Index - frameIndex) <=
                   Math.Abs(source[sourceIndex].Index - frameIndex))
                sourceIndex++;

            rebuilt.Add(new MarkerFrameResult(frameIndex, Fps, source[sourceIndex].Point));
        }

        Frames.Clear();
        Frames.AddRange(rebuilt);
    }

    public (int StartFrame, int EndFrame) RecognizedFrameRange
    {
        get
        {
            if (_timingSourceFrames is { Count: > 0 } source)
                return (source[0].Index, source[^1].Index);
            return (StartIndex(), EndIndex());
        }
    }

    public bool HasTimingEdits
    {
        get
        {
            var recognized = RecognizedFrameRange;
            return StartIndex() != recognized.StartFrame || EndIndex() != recognized.EndFrame;
        }
    }

    public void RestoreRecognizedFrameRange()
    {
        var recognized = RecognizedFrameRange;
        SetFrameRange(recognized.StartFrame, recognized.EndFrame);
    }

    public override bool IsEmpty()
    {
        return Frames.Count == 0;
    }

    public override IProcessFrame Start()
    {
        return Frames.Count > 0 ? Frames[0] : new MarkerFrameResult(0, Fps, Point.Empty);
    }

    public override IProcessFrame End()
    {
        return Frames.Count > 0 ? Frames[^1] : new MarkerFrameResult(0, Fps, Point.Empty);
    }
}
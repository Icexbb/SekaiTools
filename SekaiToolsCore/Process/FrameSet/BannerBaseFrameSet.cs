using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public class BannerBaseFrameSet(BannerStoryEvent data, FrameRate fps) : BaseFrameSet
{
    private int _start = int.MaxValue, _end = int.MinValue;
    private (int StartFrame, int EndFrame)? _recognizedFrameRange;
    public BannerStoryEvent Data { get; } = data;
    private FrameRate Fps { get; } = fps;

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public void SetFrameRange(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "结束帧不能早于起始帧");
        var wasEmpty = IsEmpty();
        if (!wasEmpty)
            _recognizedFrameRange ??= (_start, _end);
        _start = start;
        _end = end;
        if (wasEmpty)
            _recognizedFrameRange = (start, end);
    }


    public (int StartFrame, int EndFrame) RecognizedFrameRange =>
        _recognizedFrameRange ?? (StartIndex(), EndIndex());

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
        return _start == int.MaxValue && _end == int.MinValue;
    }

    public override IProcessFrame Start()
    {
        return IsEmpty() ? new ProcessFrame(0, Fps) : new ProcessFrame(_start, Fps);
    }

    public override IProcessFrame End()
    {
        return IsEmpty() ? new ProcessFrame(0, Fps) : new ProcessFrame(_end, Fps);
    }
}
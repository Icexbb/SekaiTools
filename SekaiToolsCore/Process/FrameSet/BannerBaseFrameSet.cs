using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public class BannerBaseFrameSet(BannerStoryEvent data, FrameRate fps) : BaseFrameSet
{
    private (int StartFrame, int EndFrame)? _recognizedFrameRange;
    private int _start = int.MaxValue, _end = int.MinValue;
    public bool HasRefinedTiming { get; private set; }
    public int TextStartOffset { get; private set; }
    public int TextEndOffset { get; private set; }

    public string TextStartTime() => new ProcessFrame(Math.Min(_end, _start + TextStartOffset), Fps).StartTime();
    public string TextEndTime() => new ProcessFrame(Math.Max(Math.Min(_end, _start + TextStartOffset),
        _end - TextEndOffset), Fps).EndTime();

    public void SetRefinedTiming(int maskStart, int maskEnd, int textStart, int textEnd)
    {
        if (maskStart < 0 || maskStart > textStart || textStart > textEnd || textEnd > maskEnd)
            throw new ArgumentOutOfRangeException(nameof(maskStart));
        _start = maskStart;
        _end = maskEnd;
        TextStartOffset = textStart - maskStart;
        TextEndOffset = maskEnd - textEnd;
        HasRefinedTiming = true;
        _recognizedFrameRange = (maskStart, maskEnd);
    }

    internal void RestoreUnrefinedTiming(int start, int end)
    {
        SetFrameRange(start, end);
        _recognizedFrameRange = (start, end);
        HasRefinedTiming = false;
        TextStartOffset = TextEndOffset = 0;
    }

    public BannerStoryEvent Data { get; } = data;
    private FrameRate Fps { get; } = fps;


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

using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public class BannerBaseFrameSet(BannerStoryEvent data, FrameRate fps) : BaseFrameSet
{
    private int _start = int.MaxValue, _end = int.MinValue;
    public BannerStoryEvent Data { get; } = data;
    private FrameRate Fps { get; } = fps;

    public bool Finished { get; set; }

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public void SetFrameRange(int start, int end)
    {
        _start = start;
        _end = end;
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
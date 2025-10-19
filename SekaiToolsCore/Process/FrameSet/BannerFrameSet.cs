using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process.FrameSet;

public class BannerFrameSet(Banner data, FrameRate fps) : FrameSet
{
    private int _start = int.MaxValue, _end = int.MinValue;
    public Banner Data { get; } = data;
    private FrameRate Fps { get; } = fps;

    public bool Finished { get; set; }

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public override bool IsEmpty()
    {
        return _start == int.MaxValue && _end == int.MinValue;
    }

    public override IProcessFrame Start()
    {
        return new ProcessFrame(_start, Fps);
    }

    public override IProcessFrame End()
    {
        return new ProcessFrame(_end, Fps);
    }
}
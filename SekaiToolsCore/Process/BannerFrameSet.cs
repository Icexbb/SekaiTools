using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class BannerFrameSet(Banner data, FrameRate fps) : FrameSet
{
    public Banner Data { get; } = data;
    private FrameRate Fps { get; } = fps;
    private int _start = int.MaxValue, _end = int.MinValue;

    public bool Finished { get; set; }

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public override bool IsEmpty() => _start == int.MaxValue && _end == int.MinValue;

    public override IProcessFrame Start() => new Frame(_start, Fps);

    public override IProcessFrame End() => new Frame(_end, Fps);
}
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process.FrameSet;

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

    public override IProcessFrame Start() => new ProcessFrame(_start, Fps);

    public override IProcessFrame End() => new ProcessFrame(_end, Fps);
}
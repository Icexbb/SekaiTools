using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class BannerFrameSet(Banner data, FrameRate fps)
{
    private int _start = int.MaxValue, _end = int.MinValue;

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public readonly Banner Data = data;
    public readonly FrameRate Fps = fps;
    public Frame Start() => new(_start, Fps);
    public Frame End() => new(_end, Fps);
    public string StartTime() => Start().StartTime();
    public string EndTime() => End().EndTime();

    public bool Finished { get; set; }
}
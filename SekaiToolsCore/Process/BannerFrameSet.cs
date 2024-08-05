using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class BannerFrameSet(Banner data, FrameRate fps)
{
    public readonly Banner Data = data;
    public readonly FrameRate Fps = fps;
    private int _start = int.MaxValue, _end = int.MinValue;

    public bool Finished { get; set; }

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public Frame Start()
    {
        return new Frame(_start, Fps);
    }

    public Frame End()
    {
        return new Frame(_end, Fps);
    }

    public string StartTime()
    {
        return Start().StartTime();
    }

    public string EndTime()
    {
        return End().EndTime();
    }
}
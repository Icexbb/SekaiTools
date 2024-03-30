using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class BannerFrameSet(Banner banner, FrameRate fps)
{
    private int _start = int.MaxValue, _end = int.MinValue;

    public void Add(int index)
    {
        if (_start > index) _start = index;
        if (_end < index) _end = index;
    }

    public readonly Banner Banner = banner;
    private Frame Start() => new(_start, fps);
    private Frame End() => new(_end, fps);
    public string StartTime() => Start().StartTime();
    public string EndTime() => End().EndTime();
}
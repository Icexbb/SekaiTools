using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class MarkerFrameSet(Marker data, FrameRate fps)
{
    private const int FrameIndexOffset = -1;
    public readonly Marker Data = data;
    public readonly FrameRate Fps = fps;


    public readonly List<DialogFrameResult> Frames = [];
    public bool IsEmpty => Frames.Count == 0;
    public bool Finished { get; set; }

    public void Add(int index, Rectangle rect)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, rect.Location));
    }

    public void Add(int index, Point point)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));
    }

    public DialogFrameResult Start()
    {
        return Frames[0];
    }

    public DialogFrameResult End()
    {
        return Frames[^1];
    }

    public string StartTime()
    {
        return Start().StartTime();
    }

    public string EndTime()
    {
        return End().EndTime();
    }

    public int StartIndex()
    {
        return Start().Index;
    }

    public int EndIndex()
    {
        return End().Index;
    }

    public class DialogFrameResult(int index, FrameRate fps, Point point) : Frame(index, fps)
    {
        public Point Point => point;
    }
}
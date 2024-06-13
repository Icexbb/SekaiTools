using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class MarkerFrameSet(Marker data, FrameRate fps)
{
    public class DialogFrameResult(int index, FrameRate fps, Point point) : Frame(index, fps)
    {
        public Point Point => point;
    }


    public readonly List<DialogFrameResult> Frames = [];
    public readonly Marker Data = data;
    public readonly FrameRate Fps = fps;

    private const int FrameIndexOffset = -1;
    public bool IsEmpty => Frames.Count == 0;

    public void Add(int index, Rectangle rect) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, rect.Location));

    public void Add(int index, Point point) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));

    public DialogFrameResult Start() => Frames[0];
    public DialogFrameResult End() => Frames[^1];

    public string StartTime() => Start().StartTime();

    public string EndTime() => End().EndTime();

    public int StartIndex() => Start().Index;

    public int EndIndex() => End().Index;
    public bool Finished { get; set; }
}
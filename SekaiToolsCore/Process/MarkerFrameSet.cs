using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class MarkerFrameResult(int index, FrameRate fps, Point point) : Frame(index, fps)
{
    public Point Point => point;
}

public class MarkerFrameSet(Marker data, FrameRate fps) : FrameSet
{
    private static int FrameIndexOffset { get; } = -1;
    public Marker Data { get; } = data;
    public FrameRate Fps { get; } = fps;
    public List<MarkerFrameResult> Frames { get; } = [];
    public bool Finished { get; set; }

    public void Add(int index, Point point)
    {
        Frames.Add(new MarkerFrameResult(index + FrameIndexOffset, Fps, point));
    }

    public override bool IsEmpty() => Frames.Count == 0;

    public override IProcessFrame Start() => Frames[0];

    public override IProcessFrame End() => Frames[^1];
}
using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Process.FrameSet;

public class MarkerFrameResult(int index, FrameRate fps, Point point) : ProcessFrame(index, fps)
{
    public Point Point => point;
}

public class MarkerBaseFrameSet(MarkerStoryEvent data, FrameRate fps) : BaseFrameSet
{
    public MarkerStoryEvent Data { get; } = data;
    public FrameRate Fps { get; } = fps;
    public List<MarkerFrameResult> Frames { get; } = [];
    public void Add(int index, Point point)
    {
        Frames.Add(new MarkerFrameResult(index + FrameIndexOffset, Fps, point));
    }

    public override bool IsEmpty()
    {
        return Frames.Count == 0;
    }

    public override IProcessFrame Start()
    {
        return Frames.Count > 0 ? Frames[0] : new MarkerFrameResult(0, Fps, Point.Empty);
    }

    public override IProcessFrame End()
    {
        return Frames.Count > 0 ? Frames[^1] : new MarkerFrameResult(0, Fps, Point.Empty);
    }
}
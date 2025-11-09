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
    private static int FrameIndexOffset { get; } = -1;
    public MarkerStoryEvent Data { get; } = data;
    public FrameRate Fps { get; } = fps;
    public List<MarkerFrameResult> Frames { get; } = [];
    public bool Finished { get; set; }

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
        return Frames[0];
    }

    public override IProcessFrame End()
    {
        return Frames[^1];
    }
}
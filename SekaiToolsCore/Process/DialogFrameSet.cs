using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class DialogFrameSet(Dialog dialogData, FrameRate fps)
{
    public class DialogFrameResult(int index, FrameRate fps, Rectangle nameTagRect)
        : Frame(index, fps)
    {
        public Point Point => nameTagRect.Location;
    }

    public readonly List<DialogFrameResult> Frames = [];
    public readonly Dialog DialogData = dialogData;
    private const int FrameIndexOffset = -1;

    public void Add(int index, Rectangle nameTagRect) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, fps, nameTagRect));

    public bool IsEmpty => Frames.Count == 0;

    public bool IsJitter => DialogData.Shake;

    public DialogFrameResult Start() => Frames[0];

    public DialogFrameResult End() => Frames[^1];

    public string StartTime() => Start().StartTime();

    public string EndTime() => End().EndTime();

    public int StartIndex() => Start().Index;

    public int EndIndex() => End().Index;
}
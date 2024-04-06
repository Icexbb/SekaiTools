using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class DialogFrameSet(Dialog data, FrameRate fps)
{
    public class DialogFrameResult(int index, FrameRate fps, Point point) : Frame(index, fps)
    {
        public Point Point => point;
    }

    public readonly List<DialogFrameResult> Frames = [];
    public readonly Dialog Data = data;
    public readonly FrameRate Fps = fps;
    private const int FrameIndexOffset = -1;

    public void Add(int index, Rectangle rect) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, rect.Location));

    public void Add(int index, Point point) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));

    public bool IsEmpty => Frames.Count == 0;

    public bool IsJitter => Data.Shake;

    public DialogFrameResult Start() => Frames[0];

    public DialogFrameResult End() => Frames[^1];

    public string StartTime() => Start().StartTime();

    public string EndTime() => End().EndTime();

    public int StartIndex() => Start().Index;

    public int EndIndex() => End().Index;

    public bool Finished { get; set; }


    public bool NeedSetSeparator => Data.BodyTranslated != string.Empty &&
                                    Data.BodyOriginal.LineCount() == 3 &&
                                    Data.BodyTranslated.Replace("\n", "")
                                        .Replace("\\R", "").Replace("\\N", "")
                                        .Length > 37;

    public struct Separator
    {
        public int SeparateFrame { get; set; }
        public int SeparatorContentIndex { get; set; }
    }

    public Separator Separate { get; set; }

    public void SetSeparator(int separateFrame, int separatorContentIndex)
    {
        Separate = new Separator
        {
            SeparateFrame = separateFrame,
            SeparatorContentIndex = separatorContentIndex
        };
    }
}
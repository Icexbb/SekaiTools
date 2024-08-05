using System.Drawing;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsCore.Process;

public class DialogFrameResult(int index, FrameRate fps, Point point) : Frame(index, fps)
{
    public Point Point => point;
}

public struct Separator
{
    public int SeparateFrame { get; set; }
    public int SeparatorContentIndex { get; set; }
}

public partial class DialogFrameSet
{
    private const int FrameIndexOffset = -1;
    public readonly Dialog Data;
    public readonly FrameRate Fps;
    public readonly List<DialogFrameResult> Frames = new();

    public Separator Separate;

    public DialogFrameSet(Dialog data, FrameRate fps)
    {
        Data = data;
        Fps = fps;
        UseSeparator = NeedSetSeparator;

        #region InitSeparatorContentIndex

        int separatorContentIndex;

        if (Data.BodyTranslated.Contains("\\R"))
            separatorContentIndex = Data.BodyTranslated
                .Replace("\n", "").Replace("\\N", "")
                .IndexOf("\\R", StringComparison.Ordinal);
        else if (Data.BodyTranslated.Count(c => c == '\n') == 1)
            separatorContentIndex = Data.BodyTranslated
                .IndexOf("\\R", StringComparison.Ordinal);
        else
            separatorContentIndex = Data.BodyTranslated.TrimAll().Length / 2;

        Separate.SeparatorContentIndex = separatorContentIndex;

        #endregion
    }

    public bool IsEmpty => Frames.Count == 0;

    public bool IsJitter => Data.Shake;

    public bool Finished { get; set; }

    public bool NeedSetSeparator => Data.BodyTranslated != string.Empty &&
                                    Data.BodyOriginal.LineCount() == 3 &&
                                    Data.BodyTranslated.TrimAll().Length > 37;

    public bool UseSeparator { get; set; }

    public void InitSeparator()
    {
        Separate.SeparateFrame = Utils.Middle(StartIndex() + 1, EndIndex() - 1,
            StartIndex() + Frames.Count / 2);
    }

    public void SetSeparator(int separateFrame, int separatorContentIndex)
    {
        Separate.SeparateFrame = separateFrame;
        Separate.SeparatorContentIndex = separatorContentIndex;
    }
}

public partial class DialogFrameSet
{
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

    public void Add(int index, Rectangle rect)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, rect.Location));
    }

    public void Add(int index, Point point)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));
    }
}
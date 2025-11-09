using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;

namespace SekaiToolsCore.Process.FrameSet;

public class DialogFrameResult(int index, FrameRate fps, Point point) : ProcessFrame(index, fps)
{
    public Point Point => point;
}

public struct Separator
{
    public int SeparateFrame { get; set; }
    public int SeparatorContentIndex { get; set; }
}

public partial class DialogBaseFrameSet : BaseFrameSet
{
    private const int FrameIndexOffset = -1;

    public Separator Separate;

    public DialogBaseFrameSet(DialogStoryEvent data, FrameRate fps)
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

    public DialogStoryEvent Data { get; }
    public FrameRate Fps { get; }
    public List<DialogFrameResult> Frames { get; } = [];


    public bool IsJitter => Data.Shake;

    public bool Finished { get; set; }

    public bool NeedSetSeparator => Data.BodyTranslated != string.Empty &&
                                    Data.BodyOriginal.LineCount() == 3 &&
                                    Data.BodyTranslated.TrimAll().Length > 37;

    public bool UseSeparator { get; set; }

    public void InitSeparator()
    {
        Separate.SeparateFrame = UtilFunc.Middle(StartIndex() + 1, EndIndex() - 1,
            StartIndex() + Frames.Count / 2);
    }

    public void SetSeparator(int separateFrame, int separatorContentIndex)
    {
        Separate.SeparateFrame = separateFrame;
        Separate.SeparatorContentIndex = separatorContentIndex;
    }
}

public partial class DialogBaseFrameSet
{
    public override bool IsEmpty()
    {
        return Frames.Count == 0;
    }

    public override DialogFrameResult Start()
    {
        return Frames[0];
    }

    public override DialogFrameResult End()
    {
        return Frames[^1];
    }

    public void Add(int index, Point point)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));
    }
}
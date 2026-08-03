using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;
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
    public Separator Separate;
    private List<DialogFrameResult>? _timingSourceFrames;

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

        Separate.SeparatorContentIndex = separatorContentIndex >= 0
            ? separatorContentIndex
            : Data.BodyTranslated.TrimAll().Length / 2;

        #endregion
    }

    public DialogStoryEvent Data { get; }
    public FrameRate Fps { get; }
    public List<DialogFrameResult> Frames { get; } = [];


    public bool IsJitter => Data.Shake;

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
        return Frames.Count > 0 ? Frames[0] : new DialogFrameResult(0, Fps, Point.Empty);
    }

    public override DialogFrameResult End()
    {
        return Frames.Count > 0 ? Frames[^1] : new DialogFrameResult(0, Fps, Point.Empty);
    }

    public void Add(int index, Point point)
    {
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, Fps, point));
    }

    public void SetFrameRange(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "结束帧不能早于起始帧");
        if (Frames.Count == 0)
            return;

        _timingSourceFrames ??= Frames.OrderBy(frame => frame.Index).ToList();
        var source = _timingSourceFrames;
        var rebuilt = new List<DialogFrameResult>(end - start + 1);
        var sourceIndex = 0;
        for (var frameIndex = start; frameIndex <= end; frameIndex++)
        {
            while (sourceIndex + 1 < source.Count &&
                   Math.Abs(source[sourceIndex + 1].Index - frameIndex) <=
                   Math.Abs(source[sourceIndex].Index - frameIndex))
                sourceIndex++;

            rebuilt.Add(new DialogFrameResult(frameIndex, Fps, source[sourceIndex].Point));
        }

        Frames.Clear();
        Frames.AddRange(rebuilt);
    }

    public (int StartFrame, int EndFrame) RecognizedFrameRange
    {
        get
        {
            if (_timingSourceFrames is { Count: > 0 } source)
                return (source[0].Index, source[^1].Index);
            return (StartIndex(), EndIndex());
        }
    }

    public bool HasTimingEdits
    {
        get
        {
            var recognized = RecognizedFrameRange;
            return StartIndex() != recognized.StartFrame || EndIndex() != recognized.EndFrame;
        }
    }

    public void RestoreRecognizedFrameRange()
    {
        var recognized = RecognizedFrameRange;
        SetFrameRange(recognized.StartFrame, recognized.EndFrame);
    }
}
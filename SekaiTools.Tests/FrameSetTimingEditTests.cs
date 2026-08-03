using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class FrameSetTimingEditTests
{
    [Fact]
    public void DialogRangeEdit_RebuildsFromOriginalMotionTrack()
    {
        var set = new DialogBaseFrameSet(
            new DialogStoryEvent(0, "测试", 1, "角色", false, false),
            new FrameRate(10));
        set.Add(11, new Point(10, 10));
        set.Add(12, new Point(20, 20));
        set.Add(13, new Point(30, 30));

        set.SetFrameRange(10, 10);
        set.SetFrameRange(9, 13);

        Assert.Equal([9, 10, 11, 12, 13], set.Frames.Select(frame => frame.Index));
        Assert.Equal(new Point(10, 10), set.Frames[0].Point);
        Assert.Equal(new Point(20, 20), set.Frames[2].Point);
        Assert.Equal(new Point(30, 30), set.Frames[^1].Point);
        Assert.True(set.HasTimingEdits);
        Assert.Equal((10, 12), set.RecognizedFrameRange);

        set.RestoreRecognizedFrameRange();

        Assert.False(set.HasTimingEdits);
        Assert.Equal([10, 11, 12], set.Frames.Select(frame => frame.Index));
    }

    [Fact]
    public void MarkerRangeEdit_RebuildsFromOriginalMotionTrack()
    {
        var set = new MarkerBaseFrameSet(new MarkerStoryEvent("标记", 0), new FrameRate(10));
        set.Add(6, new Point(1, 2));
        set.Add(7, new Point(3, 4));

        set.SetFrameRange(3, 8);

        Assert.Equal(3, set.StartIndex());
        Assert.Equal(8, set.EndIndex());
        Assert.Equal(new Point(1, 2), set.Frames[0].Point);
        Assert.Equal(new Point(3, 4), set.Frames[^1].Point);
        Assert.True(set.HasTimingEdits);
        Assert.Equal((5, 6), set.RecognizedFrameRange);

        set.RestoreRecognizedFrameRange();

        Assert.False(set.HasTimingEdits);
        Assert.Equal([5, 6], set.Frames.Select(frame => frame.Index));
    }


    [Fact]
    public void BannerRangeEdit_RestoresRecognizedRange()
    {
        var set = new BannerBaseFrameSet(new BannerStoryEvent("横幅", 0), new FrameRate(10));
        set.Add(5);
        set.Add(7);

        set.SetFrameRange(3, 9);

        Assert.True(set.HasTimingEdits);
        Assert.Equal((5, 7), set.RecognizedFrameRange);

        set.RestoreRecognizedFrameRange();

        Assert.False(set.HasTimingEdits);
        Assert.Equal(5, set.StartIndex());
        Assert.Equal(7, set.EndIndex());
    }

    [Fact]
    public void FrameRangeEdit_RejectsInvalidRanges()
    {
        var set = new BannerBaseFrameSet(new BannerStoryEvent("横幅", 0), new FrameRate(10));
        set.Add(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.SetFrameRange(-1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.SetFrameRange(6, 5));
    }
}

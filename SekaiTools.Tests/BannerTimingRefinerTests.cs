using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class BannerTimingRefinerTests
{
    [Fact]
    public void SeparateTextTiming_PreservesOffsetsWhenMovingAndClampsWhenShortening()
    {
        var set = new BannerBaseFrameSet(new BannerStoryEvent("地点", 0), new FrameRate(60));
        set.SetRefinedTiming(100, 200, 104, 199);
        Assert.False(set.HasTimingEdits);
        Assert.Equal(new ProcessFrame(104, new FrameRate(60)).StartTime(), set.TextStartTime());
        set.SetFrameRange(110, 210);
        Assert.Equal(new ProcessFrame(114, new FrameRate(60)).StartTime(), set.TextStartTime());
        Assert.True(set.HasTimingEdits);
        set.SetFrameRange(110, 111);
        Assert.Equal(new ProcessFrame(111, new FrameRate(60)).StartTime(), set.TextStartTime());
        Assert.Equal(new ProcessFrame(111, new FrameRate(60)).EndTime(), set.TextEndTime());
        set.RestoreRecognizedFrameRange();
        Assert.Equal(100, set.StartIndex());
        Assert.Equal(200, set.EndIndex());
        Assert.False(set.HasTimingEdits);
    }

    [Fact]
    public void LegacyRange_UsesSameTimingForTextAndMask()
    {
        var set = new BannerBaseFrameSet(new BannerStoryEvent("地点", 0), new FrameRate(60));
        set.SetFrameRange(100, 200);
        Assert.False(set.HasRefinedTiming);
        Assert.Equal(set.StartTime(), set.TextStartTime());
        Assert.Equal(set.EndTime(), set.TextEndTime());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Boundary_RejectsUnchangedSceneAndAbruptCut(bool opening)
    {
        var frames = Enumerable.Range(0, 30).Select(_ => Enumerable.Repeat(1d, 180 * 64).ToArray()).ToArray();
        Assert.Null(BannerTimingRefiner.FindBoundary(frames, opening, false));
        for (var i = 15; i < frames.Length; i++)
            for (var row = 17; row <= 49; row++)
                for (var column = 0; column < 180; column++)
                    frames[i][row * 180 + column] = 0.5;
        Assert.Null(BannerTimingRefiner.FindBoundary(frames, opening, false));
    }

    [Fact]
    public void Boundary_FollowsMeasuredFadeInsteadOfFixedPadding()
    {
        var frames = Enumerable.Range(0, 36).Select(index =>
        {
            var image = Enumerable.Repeat(1d, 180 * 64).ToArray();
            var alpha = Math.Clamp((index - 12) / 10d, 0, 1);
            for (var row = 17; row <= 49; row++)
                for (var column = 0; column < 180; column++)
                    image[row * 180 + column] -= 0.5 * alpha;
            return image;
        }).ToArray();
        Assert.Equal(13, BannerTimingRefiner.FindBoundary(frames, true, false));
        Array.Reverse(frames);
        Assert.Equal(22, BannerTimingRefiner.FindBoundary(frames, false, false));
    }
}

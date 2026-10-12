using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class MatchedFrameRecorderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DialogAndMarker_KeepEveryMatchedFrameAcrossSamplingPhases(int phase)
    {
        var dialog = new DialogBaseFrameSet(new DialogStoryEvent(0, "测试", 1, "角色", false, false), new FrameRate(10));
        var marker = new MarkerBaseFrameSet(new MarkerStoryEvent("标记", 0), new FrameRate(10));
        using var scheduler = new AdaptiveSearchScheduler();
        using var previous = new FrameMatchContext();
        scheduler.CompleteSample(phase == 0 ? 10 : 9);
        var tracking = false;
        for (var index = 11; index <= 16; index++)
        {
            if (!tracking && !scheduler.ShouldSample(index)) { scheduler.RememberSkipped(index); continue; }
            var selected = index;
            if (!tracking && scheduler.TryGetPrevious(previous, index - 1, out _, out var prior)) selected = prior;
            scheduler.CompleteSample(index);
            MatchedFrameRecorder.Record(dialog, selected, Position(selected), index, Position(index));
            MatchedFrameRecorder.Record(marker, selected, Position(selected), index, Position(index));
            tracking = true;
        }
        Assert.Equal(Enumerable.Range(10, 6), dialog.Frames.Select(f => f.Index));
        Assert.Equal(Enumerable.Range(10, 6), marker.Frames.Select(f => f.Index));
        Assert.Equal(Enumerable.Range(11, 6).Select(Position), dialog.Frames.Select(f => f.Point));
        Assert.Equal(Enumerable.Range(11, 6).Select(Position), marker.Frames.Select(f => f.Point));
    }

    [Fact]
    public void Banner_BackcheckRetainsCurrentEndAndNormalMatchDoesNotDuplicate()
    {
        var banner = new BannerBaseFrameSet(new BannerStoryEvent("横幅", 0), new FrameRate(10));
        MatchedFrameRecorder.Record(banner, 11, 12);
        Assert.Equal(11, banner.StartIndex());
        Assert.Equal(12, banner.EndIndex());
        var marker = new MarkerBaseFrameSet(new MarkerStoryEvent("标记", 0), new FrameRate(10));
        MatchedFrameRecorder.Record(marker, 12, Position(12), 12, Position(12));
        Assert.Single(marker.Frames);
    }

    private static Point Position(int index) => new(index * 2, index * 3);
}


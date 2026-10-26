using System.Drawing;
using System.Text.Json;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class DialogTrackingGraceTests
{
    [Theory]
    [InlineData(30, 2)]
    [InlineData(60, 3)]
    public void ShortLossPreservesMovingCoordinatesOnlyAfterRecovery(double fps, int limit)
    {
        var grace = new DialogTrackingGrace(fps);
        var set = CreateSet(fps);
        for (var i = 0; i < limit; i++)
            Assert.True(grace.Hold(100 + i, new Point(200 + i, 300 - i)));
        Assert.Empty(set.Frames);
        grace.Commit(set);
        Assert.Equal(Enumerable.Range(99, limit), set.Frames.Select(f => f.Index));
        Assert.Equal(Enumerable.Range(0, limit).Select(i => new Point(200 + i, 300 - i)),
            set.Frames.Select(f => f.Point));
        Assert.Empty(grace.CaptureState());
    }

    [Theory]
    [InlineData(30, 2)]
    [InlineData(60, 3)]
    public void SustainedLossAndNextTargetDoNotReceiveUnconfirmedFrames(double fps, int limit)
    {
        var grace = new DialogTrackingGrace(fps);
        var previous = CreateSet(fps);
        previous.Add(99, new Point(10, 20));
        for (var i = 0; i < limit; i++) Assert.True(grace.Hold(100 + i, new Point(i, i)));
        Assert.False(grace.Hold(100 + limit, new Point(99, 99)));
        grace.Clear();
        var next = CreateSet(fps);
        grace.Commit(next);
        Assert.Empty(next.Frames);
        Assert.Equal(98, previous.End().Index);
    }

    [Fact]
    public void JsonCheckpointPreservesGraceDeadlineAndCoordinates()
    {
        var continuous = new DialogTrackingGrace(60);
        continuous.Hold(86, new Point(10, 20));
        var json = JsonSerializer.Serialize(new DialogMatcherStateDto
        {
            Status = 4,
            PendingTrackingFrames = continuous.CaptureState()
        });
        var checkpoint = JsonSerializer.Deserialize<DialogMatcherStateDto>(json)!;
        var restored = new DialogTrackingGrace(60);
        restored.RestoreState(checkpoint.PendingTrackingFrames);
        Assert.Equal(continuous.Hold(87, new Point(11, 19)), restored.Hold(87, new Point(11, 19)));
        Assert.Equal(continuous.Hold(88, new Point(12, 18)), restored.Hold(88, new Point(12, 18)));
        Assert.False(restored.Hold(89, new Point(13, 17)));
        var first = CreateSet(60);
        var second = CreateSet(60);
        continuous.Commit(first);
        restored.Commit(second);
        Assert.Equal(first.Frames.Select(f => (f.Index, f.Point)), second.Frames.Select(f => (f.Index, f.Point)));
    }

    [Fact]
    public void LegacyCheckpointClearsPendingFrames()
    {
        var state = JsonSerializer.Deserialize<DialogMatcherStateDto>("{\"Status\":3}")!;
        var grace = new DialogTrackingGrace(60);
        grace.Hold(10, Point.Empty);
        grace.RestoreState(state.PendingTrackingFrames);
        Assert.Empty(grace.CaptureState());
        Assert.True(grace.Hold(100, new Point(1, 2)));
    }

    private static DialogBaseFrameSet CreateSet(double fps) =>
        new(new DialogStoryEvent(0, "ABC", 0, "Speaker", false, true), new FrameRate(fps));
}

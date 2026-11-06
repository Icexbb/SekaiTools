using System.Drawing;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Timing;
using Xunit;

namespace SekaiTools.Tests;

public class TimingSubtitleExporterTests
{
    [Fact]
    public void EmptyCandidatesAreSkippedAndManualTimingAndTranslationAreExported()
    {
        var rate = new FrameRate(60);
        var empty = Make(0, "");
        var filled = Make(1, "手填第一行\n第二行");
        filled.SetFrameRange(130, 170);
        var subtitle = TimingSubtitleExporter.Make(new Size(1920, 1080), "test.mp4",
            [empty, filled], new StyleFontConfig(), new ExportStyleConfig()).ToString();
        Assert.Contains("手填第一行\\N第二行", subtitle);
        Assert.Contains(filled.StartTime(), subtitle);
        Assert.Single(subtitle.Split('\n'), line => line.StartsWith("Dialogue:"));
        Assert.Equal("", filled.Data.BodyOriginal);

        DialogBaseFrameSet Make(int index, string text)
        {
            var data = new DialogStoryEvent(index, "", 0, "", false, false)
            { HasExplicitTranslation = true, BodyTranslated = text };
            var set = new DialogBaseFrameSet(data, rate);
            for (var i = 120; i < 180; i++) set.Frames.Add(new DialogFrameResult(i, rate, Point.Empty));
            return set;
        }
    }
}

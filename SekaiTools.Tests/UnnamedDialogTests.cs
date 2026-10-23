using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.Model;
using SkiaSharp;

namespace SekaiTools.Tests;

public class UnnamedDialogTests
{
    [Theory]
    [InlineData(false, "Hello")]
    [InlineData(true, "Hello")]
    [InlineData(false, "A")]
    [InlineData(false, "Hello\nWorld")]
    public void UnnamedFirstDialogueTracksDropsAndResumes(bool shake, string body)
    {
        using var fixture = new Fixture();
        var script = new SekaiToolsBase.GameScript.GameScript
        {
            TalkData = [new Talk("", body, 0) { Shake = shake }],
            Snippets = [new Snippet { Action = 1 }]
        };
        var story = new Story(script, new TranslationData(null));
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var cache = new TemplateMatchCachePool();
        using var matcher = new DialogTemplateMatcher(fixture.Info, story, manager, cache, fixture.Config);
        using var image = fixture.Frame(manager, body[..Math.Min(3, body.Length)], new Point(160, 580));
        using var context = new FrameMatchContext();
        context.Update(image);
        cache.SetFrameIndex(0);
        Assert.True(matcher.Process(context, 0), System.Text.Json.JsonSerializer.Serialize(matcher.SaveState()));
        Assert.False(matcher.Set[0].IsEmpty());
        var anchor = matcher.Set[0].Start().Point;
        Assert.InRange(anchor.X, 170, 180);
        Assert.InRange(anchor.Y, 540, 560);
        var checkpoint = matcher.SaveState();
        if (shake)
        {
            using var moved = fixture.Frame(manager, body[..Math.Min(3, body.Length)], new Point(162,582));
            context.Update(moved);
        }
        using var resumed = new DialogTemplateMatcher(fixture.Info, story, manager, new TemplateMatchCachePool(), fixture.Config);
        resumed.RestoreState(checkpoint);
        Assert.True(resumed.Process(context, 1));
        Assert.True(matcher.Process(context, 1));
        Assert.Equal(matcher.Set[0].End().Point, resumed.Set[0].End().Point);
        if (shake) Assert.NotEqual(anchor, matcher.Set[0].End().Point);
        using var blank = new Mat(image.Rows, image.Cols, DepthType.Cv8U, 3);
        blank.SetTo(new MCvScalar(0));
        context.Update(blank);
        cache.SetFrameIndex(2);
        matcher.Process(context, 2);
        Assert.True(matcher.Set[0].Finished);
        var maker = new SubtitleMaker(fixture.Info, manager, fixture.Config);
        var result = maker.Make(matcher.Set, [], [], new SubtitleExportInfo("test", "1", "done", "", "", ""), new ExportStyleConfig());
        Assert.Contains(body.Contains('\n') ? ",Line2," : ",Line1,", result.Events.ToString());
        Assert.DoesNotContain(",Character,", result.Events.ToString());
    }

    [Fact]
    public void ConsecutiveUnnamedDialoguesAdvanceWithoutSkipping()
    {
        using var fixture = new Fixture();
        var story = new Story(new SekaiToolsBase.GameScript.GameScript
        {
            TalkData = [new Talk("", "Hello", 0), new Talk("", "World", 0)],
            Snippets = [new Snippet { Action = 1 }, new Snippet { Action = 1 }]
        }, new TranslationData(null));
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var cache = new TemplateMatchCachePool();
        using var matcher = new DialogTemplateMatcher(fixture.Info, story, manager, cache, fixture.Config);
        using var first = fixture.Frame(manager, "Hel", new Point(160,580));
        using var second = fixture.Frame(manager, "Wor", new Point(160,580));
        using var context = new FrameMatchContext();
        for (var i = 0; i < 3; i++)
        {
            context.Update(first); cache.SetFrameIndex(i); matcher.Process(context, i);
        }
        for (var i = 3; i < 7; i++)
        {
            context.Update(second); cache.SetFrameIndex(i); matcher.Process(context, i);
        }
        Assert.True(matcher.Set[0].Finished);
        Assert.NotEmpty(matcher.Set[1].Frames);
        Assert.Empty(matcher.Diagnostics);
    }

    [Fact]
    public void TextOutsideDialogueAreaDoesNotAcquireUnnamedDialogue()
    {
        using var fixture = new Fixture();
        var story = new Story(new SekaiToolsBase.GameScript.GameScript
        { TalkData = [new Talk("", "Hello", 0)], Snippets = [new Snippet { Action = 1 }] }, new TranslationData(null));
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        using var matcher = new DialogTemplateMatcher(fixture.Info, story, manager, new TemplateMatchCachePool(), fixture.Config);
        using var image = fixture.Frame(manager, "Hel", new Point(160, 100));
        using var context = new FrameMatchContext();
        context.Update(image);
        Assert.False(matcher.Process(context, 0));
        Assert.Empty(matcher.Set[0].Frames);
    }

    private sealed class Fixture : ITemplateResourceProvider, IDisposable
    {
        private readonly string _video = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".avi");
        private readonly string _font = Path.GetTempFileName();
        public VideoInfo Info { get; }
        public Config Config { get; }
        public Fixture()
        {
            using var stream = SKTypeface.Default.OpenStream();
            var bytes = new byte[stream.Length];
            stream.Read(bytes, bytes.Length);
            File.WriteAllBytes(_font, bytes);
            using (var writer = new VideoWriter(_video, VideoWriter.Fourcc('M','J','P','G'), 30, new Size(1280,720), true))
            using (var frame = new Mat(720,1280,DepthType.Cv8U,3))
            {
                Assert.True(writer.IsOpened);
                frame.SetTo(new MCvScalar(0));
                writer.Write(frame);
            }
            Info = new VideoInfo(_video);
            Config = new Config(_video, _font, "");
        }
        public Mat Frame(TemplateManager manager, string text, Point position)
        {
            var frame = new Mat(720,1280,DepthType.Cv8U,3);
            frame.SetTo(new MCvScalar(0));
            using var source = manager.CreateTemplate(TemplateUsage.DialogContent, text);
            using var bgr = new Mat();
            using var resized = new Mat();
            CvInvoke.Resize(source, resized, new Size(source.Width / 5, source.Height / 5));
            CvInvoke.CvtColor(resized, bgr, ColorConversion.Bgra2Bgr);
            using var roi = new Mat(frame, new Rectangle(position, bgr.Size));
            bgr.CopyTo(roi);
            return frame;
        }
        public string GetVideoProcessResourcePath(string fileName) => _font;
        public void Dispose() { File.Delete(_video); File.Delete(_font); }
    }
}

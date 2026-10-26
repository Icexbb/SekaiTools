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
    [InlineData(false, "Speaker", false, true, false)]
    [InlineData(true, "Speaker", false, true, false)]
    [InlineData(false, "Speaker", true, false, true)]
    [InlineData(true, "Speaker", true, false, true)]
    [InlineData(false, "Translated", false, true, true)]
    [InlineData(true, "Translated", false, true, true)]
    [InlineData(false, "Translated", true, false, false)]
    [InlineData(true, "Translated", true, false, false)]
    public void SpeakerOutputCategoriesAreIndependent(bool shake, string translated, bool same, bool different, bool expected)
    {
        using var fixture = new Fixture();
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var data = new SekaiToolsBase.Story.StoryEvent.DialogStoryEvent(0, "Hello", 0, "Speaker", false, shake);
        data.SetTranslation(translated, "Hello");
        var set = new SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet(data, fixture.Info.Fps);
        set.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(0, fixture.Info.Fps, new Point(150, 500)));
        set.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(1, fixture.Info.Fps, new Point(152, 502)));
        var options = new ExportStyleConfig { ExportUnchangedCharacter = same, ExportCharacter = different };
        var result = new SubtitleMaker(fixture.Info, manager, fixture.Config).Make([set], [], [],
            new SubtitleExportInfo("test", "1", "done", "", "", ""), options);
        var dialogue = result.Events.ToString().Split('\n').Where(line => line.StartsWith("Dialogue:")).ToArray();
        Assert.Equal(expected, dialogue.Any(line => line.Contains(",Character,")));
        Assert.Contains(dialogue, line => line.Contains(",Line1,"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnnamedPositionUsesNonShakingNamedReference(bool shake)
    {
        using var fixture = new Fixture();
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var unnamed = CreateSet("", "Music", shake, 0, new Point(174, 551));
        unnamed.Data.SetTranslation("Supplement", "Music");
        unnamed.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(1, fixture.Info.Fps, new Point(180, 555)));
        var shakingNamed = CreateSet("Speaker", "Shaking", true, 10, new Point(170, 520));
        var stableNamed = CreateSet("Speaker", "Stable", false, 20, new Point(150, 500));
        var info = new SubtitleExportInfo("test", "1", "done", "", "", "");
        var baseline = new SubtitleMaker(fixture.Info, manager, fixture.Config).Make([stableNamed], [], [], info, new ExportStyleConfig());
        var actual = new SubtitleMaker(fixture.Info, manager, fixture.Config).Make([unnamed, shakingNamed, stableNamed], [], [], info, new ExportStyleConfig());
        Assert.Equal(baseline.ToString().Split('\n').Where(line => line.StartsWith("Style: Line")),
            actual.ToString().Split('\n').Where(line => line.StartsWith("Style: Line")));
        Assert.Contains(@"{\pos(160,500)}Supplement", actual.Events.ToString());
        if (shake) Assert.Contains(@"{\pos(166,504)}Supplement", actual.Events.ToString());

        SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet CreateSet(string name, string body, bool jitter, int frame, Point point)
        {
            var data = new SekaiToolsBase.Story.StoryEvent.DialogStoryEvent(frame, body, 0, name, false, jitter);
            var set = new SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet(data, fixture.Info.Fps);
            set.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(frame, fixture.Info.Fps, point));
            return set;
        }
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "译者补充的名字")]
    public void UnnamedOpeningDoesNotShiftNamedDialogueLayout(bool shake, string translatedName)
    {
        using var fixture = new Fixture();
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var unnamedData = new SekaiToolsBase.Story.StoryEvent.DialogStoryEvent(0, "Music", 0, "", false, false);
        unnamedData.SetTranslation(translatedName, "Music");
        var namedData = new SekaiToolsBase.Story.StoryEvent.DialogStoryEvent(1, "Hello", 0, "Speaker", false, shake);
        var unnamed = new SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet(unnamedData, fixture.Info.Fps);
        var named = new SekaiToolsCore.Process.FrameSet.DialogBaseFrameSet(namedData, fixture.Info.Fps);
        unnamed.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(0, fixture.Info.Fps, new Point(174, 551)));
        named.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(10, fixture.Info.Fps, new Point(150, 500)));
        named.Frames.Add(new SekaiToolsCore.Process.FrameSet.DialogFrameResult(11, fixture.Info.Fps, new Point(152, 502)));
        var info = new SubtitleExportInfo("test", "1", "done", "", "", "");
        var baseline = new SubtitleMaker(fixture.Info, manager, fixture.Config).Make([named], [], [], info, new ExportStyleConfig());
        var actual = new SubtitleMaker(fixture.Info, manager, fixture.Config).Make([unnamed, named], [], [], info, new ExportStyleConfig());

        var expectedStyles = baseline.ToString().Split('\n').Where(line => line.StartsWith("Style: Line"));
        var actualStyles = actual.ToString().Split('\n').Where(line => line.StartsWith("Style: Line"));
        Assert.Equal(expectedStyles, actualStyles);
        // 命名台词的正文位置与单独导出时相同，覆盖抖动逐帧定位标签。
        var namedLines = baseline.Events.ToString().Split('\n').Where(line => line.StartsWith("Dialogue:") && line.Contains(",Line1,"));
        foreach (var line in namedLines) Assert.Contains(line, actual.Events.ToString());
    }

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
            using var moved = fixture.Frame(manager, body[..Math.Min(3, body.Length)], new Point(162, 582));
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
        using var first = fixture.Frame(manager, "Hel", new Point(160, 580));
        using var second = fixture.Frame(manager, "Wor", new Point(160, 580));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTrackingRecoversShortLossAndRestoresPendingCheckpoint(bool shake)
    {
        using var fixture = new Fixture();
        var script = new SekaiToolsBase.GameScript.GameScript
        {
            TalkData = [new Talk("A", "WXYZ", 0) { Shake = shake }],
            Snippets = [new Snippet { Action = 1 }]
        };
        var story = new Story(script, new TranslationData(null));
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var cache = new TemplateMatchCachePool();
        using var matcher = new DialogTemplateMatcher(fixture.Info, story, manager, cache, fixture.Config);
        using var full = fixture.NamedFrame(manager, "WXY", 0);
        using var partial = fixture.NamedFrame(manager, "WX", 2);
        using var context = new FrameMatchContext();
        context.Update(full);
        for (var frame = 1; frame <= 4; frame++)
        {
            cache.SetFrameIndex(frame);
            matcher.Process(context, frame);
        }
        Assert.Equal(3, matcher.SaveState().Status);
        var count = matcher.Set[0].Frames.Count;
        context.Update(partial);
        cache.SetFrameIndex(5);
        matcher.Process(context, 5);
        var checkpoint = System.Text.Json.JsonSerializer.Deserialize<SekaiToolsCore.Process.DialogMatcherStateDto>(
            System.Text.Json.JsonSerializer.Serialize(matcher.SaveState()))!;
        Assert.Equal(4, checkpoint.Status);
        Assert.Single(checkpoint.PendingTrackingFrames);
        Assert.Equal(count, matcher.Set[0].Frames.Count);
        using var resumed = new DialogTemplateMatcher(fixture.Info, story, manager, new TemplateMatchCachePool(), fixture.Config);
        resumed.RestoreState(checkpoint);
        context.Update(full);
        cache.SetFrameIndex(6);
        matcher.Process(context, 6);
        resumed.Process(context, 6);
        Assert.Equal(3, matcher.SaveState().Status);
        Assert.Equal(count + 2, matcher.Set[0].Frames.Count);
        Assert.Equal(matcher.Set[0].Frames.Select(f => (f.Index, f.Point)), resumed.Set[0].Frames.Select(f => (f.Index, f.Point)));
        using var blank = new Mat(720, 1280, DepthType.Cv8U, 3);
        blank.SetTo(new MCvScalar(0));
        context.Update(partial);
        cache.SetFrameIndex(7);
        matcher.Process(context, 7);
        var lastReliable = matcher.Set[0].End().Index;
        context.Update(blank);
        cache.SetFrameIndex(8);
        matcher.Process(context, 8);
        Assert.True(matcher.Finished);
        Assert.Empty(matcher.SaveState().PendingTrackingFrames);
        Assert.Equal(lastReliable, matcher.Set[0].End().Index);
    }

    [Fact]
    public void SameSpeakerAndPrefixDoNotKeepPreviousDialogueAliveIndefinitely()
    {
        using var fixture = new Fixture();
        var story = new Story(new SekaiToolsBase.GameScript.GameScript
        {
            TalkData = [new Talk("A", "iiW", 0), new Talk("A", "ii.", 0)],
            Snippets = [new Snippet { Action = 1 }, new Snippet { Action = 1 }]
        }, new TranslationData(null));
        using var manager = new TemplateManager(fixture.Info.Resolution, fixture);
        var cache = new TemplateMatchCachePool();
        using var matcher = new DialogTemplateMatcher(fixture.Info, story, manager, cache, fixture.Config);
        using var first = fixture.NamedFrame(manager, "iiW", 0);
        using var next = fixture.NamedFrame(manager, "ii.", 0);
        using var context = new FrameMatchContext();
        context.Update(first);
        for (var frame = 1; frame <= 4; frame++)
        {
            cache.SetFrameIndex(frame);
            matcher.Process(context, frame);
        }
        Assert.Equal(3, matcher.SaveState().Status);
        context.Update(next);
        for (var frame = 5; frame <= 10; frame++)
        {
            cache.SetFrameIndex(frame);
            matcher.Process(context, frame);
        }
        Assert.True(matcher.Set[0].Finished);
        Assert.Equal(3, matcher.Set[0].End().Index);
        Assert.NotEmpty(matcher.Set[1].Frames);
        Assert.All(matcher.Set[1].Frames, frame => Assert.True(frame.Index >= 6));
        Assert.Equal(3, matcher.SaveState().Status);
        Assert.Empty(matcher.SaveState().PendingTrackingFrames);
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
            using (var writer = new VideoWriter(_video, VideoWriter.Fourcc('M', 'J', 'P', 'G'), 30, new Size(1280, 720), true))
            using (var frame = new Mat(720, 1280, DepthType.Cv8U, 3))
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
            var frame = new Mat(720, 1280, DepthType.Cv8U, 3);
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
        public Mat NamedFrame(TemplateManager manager, string body, int shift)
        {
            var frame = Frame(manager, body, new Point(160 + shift, 555 + shift));
            using var name = manager.CreateTemplate(TemplateUsage.DialogNameTag, "A");
            using var resized = new Mat();
            using var bgr = new Mat();
            CvInvoke.Resize(name, resized, new Size(name.Width / 5, name.Height / 5));
            CvInvoke.CvtColor(resized, bgr, ColorConversion.Bgra2Bgr);
            using var roi = new Mat(frame, new Rectangle(new Point(160 + shift, 520 + shift), bgr.Size));
            bgr.CopyTo(roi);
            return frame;
        }
        public string GetVideoProcessResourcePath(string fileName) => _font;
        public void Dispose() { File.Delete(_video); File.Delete(_font); }
    }
}

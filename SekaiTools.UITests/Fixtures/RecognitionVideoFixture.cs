using System.Drawing;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsBase.GameScript;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.UITests.Fixtures;

/// <summary>A deterministic workflow fixture, not an accuracy benchmark for real game recordings.</summary>
public static class RecognitionVideoFixture
{
    public static string Create(string directory, int dialogueEndSeconds = 2, bool timingOnly = false)
    {
        var pack = ResourcePackFixture.DirectoryFor("videoProcess");
        using var manager = new TemplateManager(new Size(1280, 720), new Resources(pack));
        using var frame = new Mat(new Size(1280, 720), DepthType.Cv8U, 3);
        frame.SetTo(new MCvScalar(25, 25, 25));
        var blank = Path.Combine(directory, "blank.png");
        frame.Save(blank);
        var menu = manager.GetMenuSign();
        Place(menu, new Point(1280 - menu.Width - 20, 20));
        using var name = manager.CreateTemplate(TemplateUsage.DialogNameTag, "测试者");
        using var body = manager.CreateTemplate(TemplateUsage.DialogContent, "测试正文。");
        using var smallName = new Mat();
        using var smallBody = new Mat();
        CvInvoke.Resize(name, smallName, new Size(name.Width / 5, name.Height / 5));
        CvInvoke.Resize(body, smallBody, new Size(body.Width / 5, body.Height / 5));
        if (timingOnly)
        {
            CvInvoke.PutText(frame, "Speaker", new Point(160, 553), HersheyFonts.Simplex, 0.55,
                new MCvScalar(255, 255, 255), 2);
            CvInvoke.Line(frame, new Point(159, 561), new Point(403, 561), new MCvScalar(140, 140, 140), 1);
            Place(smallBody, new Point(160, 580));
        }
        else
        {
            Place(smallName, new Point(160, 525));
            Place(smallBody, new Point(160, 570));
        }
        var dialogue = Path.Combine(directory, "dialogue.png");
        frame.Save(dialogue);
        var video = Path.Combine(directory, "recognition.mp4");
        FfmpegFixture.Run(Path.Combine(ResourcePackFixture.DirectoryFor("vapourSynth"), "ffmpeg.exe"), Path.Combine(directory, "generate-recognition.log"),
            "-y", "-loop", "1", "-framerate", "24", "-i", blank, "-loop", "1", "-framerate", "24", "-i", dialogue,
            "-filter_complex", $"[0:v][1:v]overlay=enable='between(t,0.5,{dialogueEndSeconds}.5)'", "-t", (dialogueEndSeconds + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:v", "libx264", "-crf", "0", "-preset", "ultrafast", "-pix_fmt", "yuv420p", video);
        if (timingOnly) return video;

        File.WriteAllText(Path.ChangeExtension(video, ".json"), JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui-recognition",
            TalkData = [new("测试者", "测试正文。", 0)],
            Snippets = [new() { Action = 1 }]
        }));
        File.WriteAllText(Path.ChangeExtension(video, ".txt"), "测试者：识别译文。");
        return video;

        void Place(Mat source, Point location)
        {
            using var bgr = new Mat();
            using var alpha = new Mat();
            CvInvoke.CvtColor(source, bgr, ColorConversion.Bgra2Bgr);
            CvInvoke.ExtractChannel(source, alpha, 3);
            using var region = new Mat(frame, new Rectangle(location, source.Size));
            bgr.CopyTo(region, alpha);
        }
    }

    private sealed class Resources(string directory) : ITemplateResourceProvider
    {
        public string GetVideoProcessResourcePath(string fileName) => Path.Combine(directory, fileName);
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase.Story;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsInfrastructure.Resources;

if (args.Length != 2) throw new ArgumentException("Usage: MaskedMatchingProbe <input-directory> <output-json>");
CvInvoke.NumThreads = 4;
var rows = new List<object>();
foreach (var video in Directory.EnumerateFiles(args[0], "*.mp4", SearchOption.AllDirectories).Order())
{
    if (!File.Exists(Path.ChangeExtension(video, ".json")) || !File.Exists(Path.ChangeExtension(video, ".txt"))) continue;
    var story = Story.FromFile(Path.ChangeExtension(video, ".json"), Path.ChangeExtension(video, ".txt"));
    var dialog = story.Dialogs().First();
    using var capture = new VideoCapture(video);
    using var frame = new Mat();
    using var gray = new Mat();
    capture.Set(CapProp.PosFrames, capture.Get(CapProp.FrameCount) / 2);
    if (!capture.Read(frame)) throw new InvalidOperationException($"Cannot read {video}");
    CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
    using var roi = new Mat(gray, new Rectangle(0, gray.Height * 3 / 4, gray.Width, gray.Height / 4));
    using var small = new Mat();
    CvInvoke.Resize(roi, small, new Size(roi.Width / 2, roi.Height / 2), interpolation: Inter.Area);
    using var manager = new TemplateManager(frame.Size, ResourceManager.Instance);
    foreach (var usage in new[] { TemplateUsage.DialogNameTag, TemplateUsage.DialogContent })
    {
        var text = usage == TemplateUsage.DialogNameTag ? dialog.CharacterOriginal : dialog.BodyOriginal.Split('\n')[0];
        if (text.Length > 16) text = text[..16];
        var template = manager.GetMatchTemplate(usage, text);
        foreach (var scale in new[] { 1d, .96, 1.04 })
        {
            var layer = template.GetScaledLayer(scale, 2);
            if (layer.Gray.Width > small.Width || layer.Gray.Height > small.Height) throw new InvalidOperationException("Probe ROI too small");
            using var floatTemplate = new Mat();
            using var binaryMask = new Mat();
            using var floatMask = new Mat();
            using var floatImage = new Mat();
            using var reference = new Mat();
            using var candidate = new Mat();
            layer.Gray.ConvertTo(floatTemplate, DepthType.Cv32F);
            // CV_8U masks select every nonzero pixel, including antialiased alpha.
            CvInvoke.Threshold(layer.Alpha, binaryMask, 0, 1, ThresholdType.Binary);
            binaryMask.ConvertTo(floatMask, DepthType.Cv32F);
            small.ConvertTo(floatImage, DepthType.Cv32F);
            CvInvoke.MatchTemplate(small, layer.Gray, reference, TemplateMatchingType.CcoeffNormed, layer.Alpha);
            CvInvoke.MatchTemplate(floatImage, floatTemplate, candidate, TemplateMatchingType.CcoeffNormed, floatMask);
            var left = ReadScores(reference);
            var right = ReadScores(candidate);
            if (!left.Select(BitConverter.SingleToInt32Bits).SequenceEqual(right.Select(BitConverter.SingleToInt32Bits)))
                throw new InvalidOperationException($"Score mismatch: {video} {usage} {scale}");
            var byteTimes = new List<double>();
            var preparedTimes = new List<double>();
            var conversionTimes = new List<double>();
            for (var round = 0; round < 3; round++)
            {
                foreach (var mode in round % 2 == 0 ? new[] { 0, 1 } : new[] { 1, 0 })
                {
                    var start = Stopwatch.GetTimestamp();
                    for (var index = 0; index < 50; index++)
                    {
                        if (mode == 0) CvInvoke.MatchTemplate(small, layer.Gray, reference, TemplateMatchingType.CcoeffNormed, layer.Alpha);
                        else
                        {
                            // Charge candidate for image conversion on every request.
                            small.ConvertTo(floatImage, DepthType.Cv32F);
                            CvInvoke.MatchTemplate(floatImage, floatTemplate, candidate, TemplateMatchingType.CcoeffNormed, floatMask);
                        }
                    }
                    (mode == 0 ? byteTimes : preparedTimes).Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / 50);
                }
                var conversionStart = Stopwatch.GetTimestamp();
                for (var index = 0; index < 50; index++) small.ConvertTo(floatImage, DepthType.Cv32F);
                conversionTimes.Add(Stopwatch.GetElapsedTime(conversionStart).TotalMilliseconds / 50);
            }
            // Recheck after all timed calls to catch unintended mutation of prepared inputs.
            if (!ReadScores(reference).Select(BitConverter.SingleToInt32Bits)
                .SequenceEqual(ReadScores(candidate).Select(BitConverter.SingleToInt32Bits))) throw new InvalidOperationException("Post-run score mismatch");
            rows.Add(new
            {
                Video = Path.GetFullPath(video),
                Usage = usage.ToString(),
                Scale = scale,
                Image = small.Size,
                Template = layer.Size,
                ScoreCount = left.Length,
                SameScoreBits = true,
                ByteMilliseconds = byteTimes.Order().ElementAt(1),
                PreparedMilliseconds = preparedTimes.Order().ElementAt(1),
                ImageConversionMilliseconds = conversionTimes.Order().ElementAt(1),
                ByteRounds = byteTimes,
                PreparedRounds = preparedTimes
            });
        }
    }
}
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Verified {rows.Count} cases");

static float[] ReadScores(Mat result)
{
    var scores = new float[result.Rows * result.Cols];
    for (var row = 0; row < result.Rows; row++) Marshal.Copy(result.DataPointer + row * result.Step, scores, row * result.Cols, result.Cols);
    return scores;
}

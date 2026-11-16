using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Timing;

public sealed record VideoTimingResult(VideoInfo Video, IReadOnlyList<DialogBaseFrameSet> Dialogs, bool Canceled);

public sealed record VideoTimingObservation(int Frame, int NamePixels, int BodyPixels,
    bool MenuPresent, bool NameLinePresent, bool Visible, double RetainedFraction);

/// <summary>Finds dialogue intervals using visual changes, without a script or OCR.</summary>
public sealed class VideoTimingRecognizer
{
    public VideoTimingResult Recognize(string videoPath, string menuTemplatePath,
        Action<DialogBaseFrameSet>? onDialog = null, Action<double>? onProgress = null,
        CancellationToken cancellationToken = default,
        Action<Mat, int, VideoInfo>? onPreview = null, Func<bool>? isPreviewEnabled = null,
        Action<VideoTimingObservation>? onObservation = null)
    {
        Logger.Log("开始纯视频时间轴识别");
        var info = new VideoInfo(videoPath);
        using var capture = new VideoCapture(videoPath);
        using var managerMenu = CvInvoke.Imread(menuTemplatePath, ImreadModes.Unchanged);
        using var resizedMenu = new Mat();
        var menuSize = TemplateManager.GetMenuSignSize(info.Resolution);
        CvInvoke.Resize(managerMenu, resizedMenu, new Size(menuSize, menuSize));
        using var menu = new GaMat(resizedMenu, false);
        var w = info.Resolution.Width;
        var h = info.Resolution.Height;
        var ratio = w / (double)h;
        var split = ratio < 1.9 ? 1 - .12 * Math.Min(ratio, 16d / 9) : .79;
        var left = ratio < 1.9 ? .10 : .17;
        var top = ratio < 1.9 ? split - .05 : .72;
        var right = ratio < 1.9 ? .90 : .86;
        var bottom = ratio < 1.9 ? .95 : .92;
        var rect = Rectangle.FromLTRB((int)(left * w), (int)(top * h), (int)(right * w), (int)(bottom * h));
        var scale = 1280d / rect.Width;
        var boundary = (int)Math.Round((split - top) * h * scale);
        var step = Math.Max(1, (int)Math.Round(info.Fps.Fps() / 12));
        var dialogs = new List<DialogBaseFrameSet>();
        var start = -1;
        var lastVisible = -1;
        var pendingReset = -1;
        long lastPreviewTimestamp = 0;
        Mat? previous = null;
        using var frame = new Mat();
        try
        {
            for (var index = 0; index < info.FrameCount; index++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!capture.Grab()) throw new IOException($"视频在第 {index} 帧提前结束");
                if (index % step != 0) continue;
                if (!capture.Retrieve(frame) || frame.IsEmpty) throw new IOException($"无法读取第 {index} 帧");
                using var cropView = new Mat(frame, rect);
                using var crop = new Mat();
                CvInvoke.Resize(cropView, crop, new Size(1280, (int)Math.Round(rect.Height * scale)));
                using var nameRegion = new Mat(crop, new Rectangle(0, 0, crop.Width, boundary));
                using var nameLeft = new Mat(nameRegion, new Rectangle(0, 0, 576, boundary));
                using var bodyRegion = new Mat(crop, new Rectangle(0, boundary, crop.Width, crop.Height - boundary));
                using var name = TextMask(nameLeft);
                using var body = TextMask(bodyRegion);
                var namePixels = CvInvoke.CountNonZero(name);
                var bodyPixels = CvInvoke.CountNonZero(body);
                var enoughPixels = namePixels >= 30 && bodyPixels >= 35;
                var menuPresent = (enoughPixels || onObservation != null) && HasMenu(frame, menu);
                var nameLinePresent = ((enoughPixels && menuPresent) || onObservation != null) && HasNameLine(nameRegion);
                var visible = enoughPixels && menuPresent && nameLinePresent;
                var retained = previous != null && (visible || onObservation != null) ? Retained(previous, body) : 1;
                onObservation?.Invoke(new VideoTimingObservation(index, namePixels, bodyPixels,
                    menuPresent, nameLinePresent, visible, retained));
                var reset = visible && previous != null && retained < .65;
                if (visible)
                {
                    if (start < 0) start = index;
                    else if (reset)
                    {
                        if (pendingReset < 0) pendingReset = index;
                    }
                    else if (pendingReset >= 0)
                    {
                        var next = pendingReset;
                        Finish(next);
                        start = next;
                    }
                    previous?.Dispose();
                    previous = body.Clone();
                    lastVisible = index;
                }
                else if (start >= 0 && index - lastVisible >= step * 2)
                {
                    Finish(lastVisible + step);
                    previous?.Dispose();
                    previous = null;
                }
                onProgress?.Invoke((index + 1d) / info.FrameCount);
                if (onPreview != null && (isPreviewEnabled?.Invoke() ?? true) &&
                    (lastPreviewTimestamp == 0 || Stopwatch.GetElapsedTime(lastPreviewTimestamp).TotalMilliseconds >= 200))
                {
                    // The callback borrows this Mat synchronously; no unbounded frame queue.
                    using var preview = new Mat();
                    var previewScale = Math.Min(1, 960d / frame.Width);
                    CvInvoke.Resize(frame, preview, new Size((int)(frame.Width * previewScale), (int)(frame.Height * previewScale)));
                    onPreview(preview, index, info);
                    lastPreviewTimestamp = Stopwatch.GetTimestamp();
                }
            }
            Finish(Math.Min(info.FrameCount, lastVisible + step));
        }
        finally
        {
            previous?.Dispose();
        }
        Logger.Log($"纯视频时间轴识别结束: 候选={dialogs.Count}, 已取消={cancellationToken.IsCancellationRequested}");
        return new VideoTimingResult(info, dialogs, cancellationToken.IsCancellationRequested);

        void Finish(int exclusiveEnd)
        {
            if (start >= 0 && exclusiveEnd - start >= info.Fps.Fps() * .25)
            {
                var data = new DialogStoryEvent(dialogs.Count, "", 0, "", false, false)
                { HasExplicitTranslation = true };
                var set = new DialogBaseFrameSet(data, info.Fps) { Finished = true };
                for (var i = start; i < exclusiveEnd; i++)
                    set.Frames.Add(new DialogFrameResult(i, info.Fps, rect.Location));
                dialogs.Add(set);
                onDialog?.Invoke(set);
            }
            start = -1;
            pendingReset = -1;
        }
    }

    private static bool HasMenu(Mat frame, GaMat menu)
    {
        using var region = new Mat(frame, new Rectangle(frame.Width - menu.Size.Width * 3, 0,
            menu.Size.Width * 3, menu.Size.Height * 2));
        using var gray = new Mat();
        using var result = new Mat();
        CvInvoke.CvtColor(region, gray, ColorConversion.Bgr2Gray);
        CvInvoke.MatchTemplate(gray, menu.Gray, result, TemplateMatchingType.CcoeffNormed, menu.Alpha);
        var values = new float[result.Rows * result.Cols];
        Marshal.Copy(result.DataPointer, values, 0, values.Length);
        return values.Any(value => float.IsFinite(value) && value >= .85);
    }

    private static bool HasNameLine(Mat region)
    {
        using var gray = new Mat();
        using var edges = new Mat();
        using var horizontal = new Mat();
        using var kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(120, 1), new Point(-1, -1));
        CvInvoke.CvtColor(region, gray, ColorConversion.Bgr2Gray);
        CvInvoke.Canny(gray, edges, 10, 30);
        CvInvoke.MorphologyEx(edges, horizontal, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, default);
        using var labels = new Mat();
        using var stats = new Mat();
        using var centers = new Mat();
        var count = CvInvoke.ConnectedComponentsWithStats(horizontal, labels, stats, centers);
        var data = new int[count * 5];
        Marshal.Copy(stats.DataPointer, data, 0, data.Length);
        for (var i = 1; i < count; i++)
            if (data[i * 5] < 256 && data[i * 5 + 1] >= region.Height * .4 && data[i * 5 + 2] >= 120)
                return true;
        return false;
    }

    private static Mat TextMask(Mat region)
    {
        using var image = region.Clone();
        var data = new byte[region.Width * region.Height * 3];
        Marshal.Copy(image.DataPointer, data, 0, data.Length);
        using var brightImage = new Mat(region.Size, DepthType.Cv8U, 1);
        var bright = new byte[region.Width * region.Height];
        for (var i = 0; i < bright.Length; i++)
        {
            var min = Math.Min(data[i * 3], Math.Min(data[i * 3 + 1], data[i * 3 + 2]));
            var max = Math.Max(data[i * 3], Math.Max(data[i * 3 + 1], data[i * 3 + 2]));
            if (min > 175 && max - min < 65) bright[i] = 255;
        }
        Marshal.Copy(bright, 0, brightImage.DataPointer, bright.Length);
        using var labels = new Mat();
        using var stats = new Mat();
        using var centers = new Mat();
        var count = CvInvoke.ConnectedComponentsWithStats(brightImage, labels, stats, centers);
        var statistics = new int[count * 5];
        Marshal.Copy(stats.DataPointer, statistics, 0, statistics.Length);
        var valid = new bool[count];
        for (var i = 1; i < count; i++)
            valid[i] = statistics[i * 5 + 2] is >= 2 and <= 70 && statistics[i * 5 + 3] is >= 4 and <= 65 &&
                       statistics[i * 5 + 4] is >= 5 and <= 2500;
        var ids = new int[region.Width * region.Height];
        Marshal.Copy(labels.DataPointer, ids, 0, ids.Length);
        var bytes = new byte[ids.Length];
        for (var i = 0; i < ids.Length; i++) if (valid[ids[i]]) bytes[i] = 255;
        var result = new Mat(region.Size, DepthType.Cv8U, 1);
        Marshal.Copy(bytes, 0, result.DataPointer, bytes.Length);
        return result;
    }

    private static double Retained(Mat previous, Mat current)
    {
        var count = CvInvoke.CountNonZero(previous);
        if (count == 0) return 1;
        using var expanded = new Mat();
        using var intersection = new Mat();
        using var kernel = CvInvoke.GetStructuringElement(MorphShapes.Rectangle, new Size(5, 5), new Point(-1, -1));
        CvInvoke.Dilate(current, expanded, kernel, new Point(-1, -1), 1, BorderType.Constant, default);
        CvInvoke.BitwiseAnd(previous, expanded, intersection);
        return CvInvoke.CountNonZero(intersection) / (double)count;
    }
}

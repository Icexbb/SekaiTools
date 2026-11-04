using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Match.TemplateMatcher;

/// <summary>
/// Rechecks only the edges of a confirmed banner, using its own background and stable image.
/// The independent reader never seeks the processing capture or changes its frame numbering.
/// </summary>
internal sealed class BannerTimingRefiner(string videoPath, double fps) : IDisposable
{
    private const int Width = 180;
    private const int Height = 64;
    private VideoCapture? _capture;

    public void Dispose() => _capture?.Dispose();

    public void Refine(BannerBaseFrameSet set, CancellationToken token)
    {
        if (set.IsEmpty() || set.HasTimingEdits || set.HasRefinedTiming ||
            set.EndIndex() - set.StartIndex() < fps || fps is < 10 or > 240) return;
        token.ThrowIfCancellationRequested();
        _capture ??= new VideoCapture(videoPath);
        if (!_capture.IsOpened) return;
        var radius = (int)Math.Ceiling(fps * 0.3);
        var start = Math.Max(0, set.StartIndex() - radius);
        var end = Math.Max(0, set.EndIndex() - radius);
        var opening = Read(start, set.StartIndex() + radius, token);
        var closing = Read(end, set.EndIndex() + (int)Math.Ceiling(fps * 0.15), token);
        if (opening == null || closing == null) return;
        var maskStart = FindBoundary(opening, true, false);
        var textStart = FindBoundary(opening, true, true);
        var maskEnd = FindBoundary(closing, false, false);
        var textEnd = FindBoundary(closing, false, true);
        // Require all four independent measurements; partial evidence must not alter an event.
        if (maskStart == null || textStart == null || maskEnd == null || textEnd == null) return;
        var ms = start + maskStart.Value;
        var ts = start + textStart.Value;
        var me = end + maskEnd.Value;
        var te = end + textEnd.Value;
        if (ms > ts || te > me || ts > te || ms > set.StartIndex() || me < set.EndIndex()) return;
        set.SetRefinedTiming(ms, me, ts, te);
    }

    private List<double[]>? Read(int first, int last, CancellationToken token)
    {
        if (!_capture!.Set(CapProp.PosFrames, first) ||
            Math.Abs(_capture.Get(CapProp.PosFrames) - first) > 0.5) return null;
        var result = new List<double[]>();
        using var source = new Mat();
        using var gray = new Mat();
        using var small = new Mat();
        for (var index = first; index <= last; index++)
        {
            token.ThrowIfCancellationRequested();
            if (!_capture.Read(source) || source.IsEmpty) return null;
            var w = (int)Math.Round(source.Width * 0.3);
            var h = (int)Math.Round(source.Width * Height / 600d);
            if (h > source.Height || w < Width || h < Height) return null;
            using var roi = new Mat(source, new Rectangle((source.Width - w) / 2,
                (source.Height - h) / 2, w, h));
            CvInvoke.CvtColor(roi, gray, ColorConversion.Bgr2Gray);
            CvInvoke.Resize(gray, small, new Size(Width, Height), interpolation: Inter.Area);
            var bytes = new byte[Width * Height];
            for (var row = 0; row < Height; row++)
                Marshal.Copy(small.DataPointer + row * small.Step, bytes, row * Width, Width);
            var mean = bytes.Average(value => (double)value);
            if (mean < 8) return null;
            // Suppress whole-scene brightness fades without removing the local banner transition.
            result.Add(bytes.Select(value => value / mean).ToArray());
        }
        return result;
    }

    internal static int? FindBoundary(IReadOnlyList<double[]> frames, bool opening, bool text)
    {
        if (frames.Count < 9 || frames.Any(frame => frame.Length != Width * Height)) return null;
        var images = text ? frames.Select(Edges).ToArray() : frames.ToArray();
        // Use the nearest clean background, after any whole-scene opening fade has settled.
        var background = opening ? images[frames.Count / 3 - 1] : images[^1];
        var foreground = opening ? images[^1] : images[0];
        var pixels = Enumerable.Range(0, Width * Height).Where(index =>
        {
            var row = index / Width;
            var column = index % Width;
            return text
                ? row is >= 22 and <= 43 && column is >= 24 and < Width - 24 &&
                  foreground[index] - background[index] > 0.25
                : (row is >= 17 and <= 20 or >= 46 and <= 49) &&
                  background[index] - foreground[index] > 0.12;
        }).ToArray();
        if (pixels.Length < 40) return null;
        var scores = images.Select(image => Score(image)).ToArray();
        var noise = opening ? scores.Take(scores.Length / 3).ToArray() : scores.TakeLast(3).ToArray();
        if (noise.Max() - noise.Min() > 0.35) return null;
        var threshold = Math.Max(0.015, noise.TakeLast(3).Max() + (opening ? 0.025 : 0.015));
        if (threshold > 0.4) return null;
        // Abrupt cuts and an unstable reference are not evidence of a banner fade.
        var stable = opening ? scores.TakeLast(3) : scores.Take(3);
        if (stable.Any(value => Math.Abs(value - 1) > 0.15) ||
            scores.Count(value => value > threshold + 0.05 && value < 0.9) < 3 ||
            !text && scores.Zip(scores.Skip(1)).Any(pair => Math.Abs(pair.First - pair.Second) > 0.45))
            return null;
        if (opening)
        {
            for (var i = noise.Length; i < scores.Length - 3; i++)
                if (scores[i] > threshold && scores[i + 1] > threshold && scores[i + 2] > threshold)
                    return i;
        }
        else
        {
            for (var i = scores.Length - 4; i >= 2; i--)
                if (scores[i] > threshold && scores[i - 1] > threshold && scores[i - 2] > threshold)
                    return i;
        }
        return null;

        double Score(double[] image)
        {
            var best = double.NegativeInfinity;
            // The game slides text while fading it in. Compare shifted glyphs, not just their final position.
            for (var shift = text && opening ? -20 : 0; shift <= (text && opening ? 2 : 0); shift++)
            {
                var values = pixels.Select(index => (image[index + shift] - background[index + shift]) /
                                                     (foreground[index] - background[index])).ToArray();
                Array.Sort(values);
                best = Math.Max(best, values[(int)((values.Length - 1) * (text ? 0.5 : 0.8))]);
            }
            return best;
        }
    }

    private static double[] Edges(double[] image)
    {
        var result = new double[image.Length];
        for (var i = 0; i < image.Length; i++)
            if (i % Width is >= 3 and < Width - 3)
                result[i] = image[i] - (image[i - 3] + image[i + 3]) / 2;
        return result;
    }
}

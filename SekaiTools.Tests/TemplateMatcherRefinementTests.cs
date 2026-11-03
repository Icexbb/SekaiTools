using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Model;
using Matcher = SekaiToolsCore.Match.TemplateMatcher.TemplateMatcher;

namespace SekaiTools.Tests;

public class TemplateMatcherRefinementTests
{
    [Fact]
    public void Match_RefinesScaledCandidateAtOriginalResolution()
    {
        using var templateSource = CreateTemplate();
        using var templateBgr = new Mat();
        CvInvoke.CvtColor(templateSource, templateBgr, ColorConversion.Bgra2Bgr);
        using var frame = new Mat(70, 90, DepthType.Cv8U, 3);
        frame.SetTo(new MCvScalar(12, 12, 12));
        var expected = new Point(31, 19);
        using (var target = new Mat(frame, new Rectangle(expected, templateBgr.Size)))
        {
            templateBgr.CopyTo(target);
        }

        using var context = new FrameMatchContext();
        context.Update(frame);
        using var template = new GaMat(templateSource, false);
        var cache = new TemplateMatchCachePool();
        cache.SetFrameIndex(1);

        var result = Matcher.Match(context, new Rectangle(Point.Empty, frame.Size), template, cache);

        Assert.Equal(expected, result.MaxLoc);
        Assert.True(result.MaxVal > 0.99);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRefinementRequiresStrongEvidenceFromDistinctCandidate(bool weakCandidate)
    {
        using var gray = new Mat(32, 32, DepthType.Cv8U, 1);
        var pixels = new byte[32 * 32];
        new Random(42).NextBytes(pixels);
        Marshal.Copy(pixels, 0, gray.DataPointer, pixels.Length);
        using var source = new Mat();
        CvInvoke.CvtColor(gray, source, ColorConversion.Gray2Bgra);
        using var template = new GaMat(source, false);
        using var small = new Mat();
        using var distractor = new Mat();
        CvInvoke.Resize(gray, small, new Size(16, 16), interpolation: Inter.Area);
        CvInvoke.Resize(small, distractor, gray.Size, interpolation: Inter.Nearest);
        using var image = new Mat(120, 180, DepthType.Cv8U, 1);
        image.SetTo(new MCvScalar(12));
        using (var roi = new Mat(image, new Rectangle(10, 10, 32, 32)))
        {
            distractor.CopyTo(roi);
        }

        var expected = new Point(91, 51);
        using (var roi = new Mat(image, new Rectangle(expected, gray.Size)))
        {
            gray.CopyTo(roi);
            if (weakCandidate)
            {
                var noise = new byte[pixels.Length];
                new Random(84).NextBytes(noise);
                var blended = pixels.Select((p, i) => (byte)(p * 0.6 + noise[i] * 0.4)).ToArray();
                using var noisy = new Mat(32, 32, DepthType.Cv8U, 1);
                Marshal.Copy(blended, 0, noisy.DataPointer, blended.Length);
                noisy.CopyTo(roi);
            }
        }

        using var context = new FrameMatchContext();
        context.Update(image);
        var pool = new TemplateMatchCachePool();
        const TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.DialogContent3;
        pool.ObserveScale(usage, 1, 0.99);
        pool.SetFrameIndex(1);
        var region = new Rectangle(Point.Empty, image.Size);
        var threshold = weakCandidate ? 0.7 : 0.95;
        var original = Matcher.Match(context, region, template, pool, usage);
        Assert.False(original.IsMatch(threshold));
        var result = Matcher.MatchContent(context, region, template, pool, usage, threshold);
        Assert.Equal(!weakCandidate, result.IsMatch(threshold));
        if (!weakCandidate) Assert.Equal(expected, result.MaxLoc);
    }

    private static Mat CreateTemplate()
    {
        var template = new Mat(20, 24, DepthType.Cv8U, 4);
        template.SetTo(new MCvScalar(20, 20, 20, 255));
        CvInvoke.Rectangle(template, new Rectangle(3, 2, 7, 14),
            new MCvScalar(240, 240, 240, 255), -1);
        CvInvoke.Rectangle(template, new Rectangle(13, 6, 8, 5),
            new MCvScalar(130, 130, 130, 255), -1);
        return template;
    }
}
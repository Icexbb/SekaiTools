using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Model;
using Matcher = SekaiToolsCore.Match.TemplateMatcher.TemplateMatcher;

namespace SekaiTools.Tests;

public class DialogScaleRecoveryTests
{
    [Fact]
    public void TrackingRecoversDifferentScaleEvenAfterWeakResultWasCached()
    {
        using var gray = new Mat(80, 200, DepthType.Cv8U, 1);
        var pixels = new byte[80 * 200];
        new Random(42).NextBytes(pixels);
        Marshal.Copy(pixels, 0, gray.DataPointer, pixels.Length);
        using var bgra = new Mat();
        CvInvoke.CvtColor(gray, bgra, ColorConversion.Gray2Bgra);
        using var template = new GaMat(bgra, false);
        var scaled = template.GetScaledLayer(0.96, 1);
        using var image = new Mat(160, 300, DepthType.Cv8U, 1);
        image.SetTo(new MCvScalar(12));
        using (var target = new Mat(image, new Rectangle(new Point(32, 20), scaled.Size)))
            scaled.Gray.CopyTo(target);
        using var frame = new FrameMatchContext();
        frame.Update(image);
        var pool = new TemplateMatchCachePool();
        const TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.DialogContent3;
        pool.ObserveScale(usage, 1, 0.99);
        pool.SetFrameIndex(1);
        var region = new Rectangle(Point.Empty, image.Size);
        var original = Matcher.MatchContent(frame, region, template, pool, usage, 0.752);
        Assert.False(original.IsMatch(0.752));

        var recovered = Matcher.MatchContent(frame, region, template, pool, usage, 0.752, allowScaleRecovery: true);

        Assert.True(recovered.MaxVal > 0.99);
        Assert.Equal(0.96, recovered.Scale);
        Assert.Equal(new Point(32, 20), recovered.MaxLoc);
        Assert.Equal([0.96], pool.GetCandidateScales(usage));
        var restored = new TemplateMatchCachePool();
        restored.RestoreScaleState(JsonSerializer.Deserialize<List<TemplateScaleStateDto>>(
            JsonSerializer.Serialize(pool.CaptureScaleState())));
        restored.SetFrameIndex(2);
        var next = Matcher.MatchContent(frame, region, template, restored, usage, 0.752, allowScaleRecovery: true);
        Assert.Equal(recovered.MaxVal, next.MaxVal);
        Assert.Equal(recovered.MaxLoc, next.MaxLoc);
        Assert.Equal(pool.CaptureScaleState(), restored.CaptureScaleState());

        image.SetTo(new MCvScalar(12));
        frame.Update(image);
        restored.SetFrameIndex(3);
        var absent = Matcher.MatchContent(frame, region, template, restored, usage, 0.752, allowScaleRecovery: true);
        Assert.False(absent.IsMatch(0.752));
        Assert.Equal([0.96], restored.GetCandidateScales(usage));
    }

    [Theory]
    [InlineData(0.96, 0.80, 0.752)]
    [InlineData(0.96, 0.90, 0.95)]
    [InlineData(1.08, 0.99, 0.752)]
    [InlineData(0.96, double.NaN, 0.752)]
    public void WeakUnsupportedOrBelowConfiguredThresholdEvidenceCannotMoveLock(double scale, double score, double threshold)
    {
        var calibration = new TemplateScaleCalibration();
        calibration.Observe(1, 0.99);
        Assert.False(calibration.TryRecover(scale, score, threshold));
        Assert.Equal([1.0], calibration.CandidateScales);
    }
}

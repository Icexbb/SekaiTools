using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Model;
using Matcher = SekaiToolsCore.Match.TemplateMatcher.TemplateMatcher;

namespace SekaiTools.Tests;

public class ContentPositionHintTests
{
    [Fact]
    public void MovingAndDisappearingContentFallsBackWithoutChangingPresenceDecision()
    {
        using var source = new Mat(20, 24, DepthType.Cv8U, 4);
        source.SetTo(new MCvScalar(20, 20, 20, 255));
        CvInvoke.Rectangle(source, new Rectangle(3, 2, 7, 14), new MCvScalar(240, 240, 240, 255), -1);
        CvInvoke.Rectangle(source, new Rectangle(13, 6, 8, 5), new MCvScalar(130, 130, 130, 255), -1);
        using var template = new GaMat(source, false);
        using var color = new Mat();
        CvInvoke.CvtColor(source, color, ColorConversion.Bgra2Bgr);
        using var frame = new Mat(120, 240, DepthType.Cv8U, 3);
        using var context = new FrameMatchContext();
        var reference = new TemplateMatchCachePool();
        var optimized = new TemplateMatchCachePool { UseContentPositionHints = true };
        var region = new Rectangle(Point.Empty, frame.Size);
        Point?[] positions = [new(20, 30), new(21, 31), new(170, 60), null, new(5, 8)];
        for (var index = 0; index < positions.Length; index++)
        {
            frame.SetTo(new MCvScalar(12, 12, 12));
            if (positions[index] is { } point)
            {
                using var roi = new Mat(frame, new Rectangle(point, color.Size));
                color.CopyTo(roi);
            }
            context.Update(frame);
            reference.SetFrameIndex(index);
            optimized.SetFrameIndex(index);
            var expected = Matcher.Match(context, region, template, reference, TemplateMatchCachePool.MatchUsage.DialogContent3);
            var actual = Matcher.MatchContent(context, region, template, optimized, TemplateMatchCachePool.MatchUsage.DialogContent3, 0.8);
            Assert.Equal(expected.IsMatch(0.8), actual.IsMatch(0.8));
            Assert.Equal(positions[index].HasValue, actual.IsMatch(0.8));
            if (positions[index].HasValue) Assert.Equal(expected.MaxLoc, actual.MaxLoc);
        }
    }
}

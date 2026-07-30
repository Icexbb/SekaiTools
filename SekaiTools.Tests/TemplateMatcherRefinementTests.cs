using System.Drawing;
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
            templateBgr.CopyTo(target);

        using var context = new FrameMatchContext();
        context.Update(frame);
        using var template = new GaMat(templateSource, false);
        var cache = new TemplateMatchCachePool();
        cache.SetFrameIndex(1);

        var result = Matcher.Match(context, new Rectangle(Point.Empty, frame.Size), template, cache);

        Assert.Equal(expected, result.MaxLoc);
        Assert.True(result.MaxVal > 0.99);
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

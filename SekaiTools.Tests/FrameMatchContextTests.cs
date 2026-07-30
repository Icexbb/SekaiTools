using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;

namespace SekaiTools.Tests;

public class FrameMatchContextTests
{
    [Fact]
    public void GetScaledGrayRoi_ReusesLayerWithinCurrentFrame()
    {
        using var source = new Mat(20, 20, DepthType.Cv8U, 3);
        source.SetTo(new MCvScalar(10, 20, 30));
        using var context = new FrameMatchContext();
        context.Update(source);
        var region = new Rectangle(2, 4, 12, 10);

        var first = context.GetScaledGrayRoi(region, 2, Inter.Area);
        var second = context.GetScaledGrayRoi(region, 2, Inter.Area);

        Assert.Same(first, second);
        Assert.Equal(new Size(6, 5), first.Size);
        Assert.Equal(1, context.Gray.NumberOfChannels);
    }

    [Fact]
    public void Update_InvalidatesScaledLayers()
    {
        using var source = new Mat(20, 20, DepthType.Cv8U, 3);
        using var context = new FrameMatchContext();
        var region = new Rectangle(0, 0, 10, 10);
        context.Update(source);
        var first = context.GetScaledGrayRoi(region, 2, Inter.Area);

        context.Update(source);
        var second = context.GetScaledGrayRoi(region, 2, Inter.Area);

        Assert.NotSame(first, second);
    }
}

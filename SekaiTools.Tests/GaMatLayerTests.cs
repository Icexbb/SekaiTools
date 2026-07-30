using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class GaMatLayerTests
{
    [Fact]
    public void GetScaledLayer_ReusesGrayAndAlphaMats()
    {
        using var source = new Mat(80, 100, DepthType.Cv8U, 4);
        source.SetTo(new MCvScalar(100, 150, 200, 255));
        using var template = new GaMat(source);

        var first = template.GetScaledLayer(2);
        var second = template.GetScaledLayer(2);

        Assert.Same(first.Gray, second.Gray);
        Assert.Same(first.Alpha, second.Alpha);
        Assert.Equal(new Size(10, 8), first.Size);
    }

    [Fact]
    public void GetScaledLayer_SeparatesDifferentDivisors()
    {
        using var source = new Mat(100, 100, DepthType.Cv8U, 4);
        source.SetTo(new MCvScalar(100, 150, 200, 255));
        using var template = new GaMat(source);

        var half = template.GetScaledLayer(2);
        var quarter = template.GetScaledLayer(4);

        Assert.NotSame(half.Gray, quarter.Gray);
        Assert.Equal(new Size(10, 10), half.Size);
        Assert.Equal(new Size(5, 5), quarter.Size);
    }
}

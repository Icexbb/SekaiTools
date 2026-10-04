using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Utils;

namespace SekaiTools.Tests;

public class TemplateMatchResultTests
{
    [Fact]
    public void CleanupPreservesFiniteScoresAndRespectsRoiStride()
    {
        using var parent = new Mat(3, 40, DepthType.Cv32F, 1);
        parent.SetTo(new MCvScalar(0.75));
        using var scores = new Mat(parent, new Rectangle(2, 1, 35, 2));
        var values = Enumerable.Range(0, 35).Select(i => (i % 5) switch
        {
            0 => float.NaN, 1 => float.PositiveInfinity, 2 => float.NegativeInfinity,
            3 => float.MaxValue, _ => -0.5f
        }).ToArray();
        for (var row = 0; row < 2; row++)
            System.Runtime.InteropServices.Marshal.Copy(values, 0, scores.DataPointer + row * scores.Step, values.Length);
        scores.MatRemoveErrorInf();
        for (var row = 0; row < 2; row++)
        {
            var actual = new float[35];
            System.Runtime.InteropServices.Marshal.Copy(scores.DataPointer + row * scores.Step, actual, 0, 35);
            Assert.Equal(values.Select(v => float.IsFinite(v) ? v : 0), actual);
        }
        var surrounding = new float[40];
        System.Runtime.InteropServices.Marshal.Copy(parent.DataPointer, surrounding, 0, 40);
        Assert.All(surrounding, value => Assert.Equal(0.75f, value));
    }

    [Fact]
    public void IsMatch_AcceptsPerfectFiniteMatch()
    {
        var result = new TemplateMatchResult(1, 0, Point.Empty, Point.Empty);

        Assert.True(result.IsMatch(0.8));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsMatch_RejectsNonFiniteScores(double score)
    {
        var result = new TemplateMatchResult(score, 0, Point.Empty, Point.Empty);

        Assert.False(result.IsMatch(0.8));
    }

    [Fact]
    public void IsMatch_RequiresScoreAboveThreshold()
    {
        var result = new TemplateMatchResult(0.8, 0, Point.Empty, Point.Empty);

        Assert.False(result.IsMatch(0.8));
    }

    [Fact]
    public void MatRemoveErrorInf_PreservesPerfectCorrelation()
    {
        using var scores = new Mat(1, 1, DepthType.Cv32F, 1);
        scores.SetTo(new MCvScalar(1));

        scores.MatRemoveErrorInf();
        double min = 0, max = 0;
        Point minLocation = default, maxLocation = default;
        CvInvoke.MinMaxLoc(scores, ref min, ref max, ref minLocation, ref maxLocation);

        Assert.Equal(1, max);
    }
}

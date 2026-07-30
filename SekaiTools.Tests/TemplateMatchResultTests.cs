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

using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class TemplateMatchCachePoolTests
{
    [Fact]
    public void TryGet_DoesNotReuseResultForDifferentTemplateOrPool()
    {
        using var image = new Mat(16, 16, DepthType.Cv8U, 1);
        using var templateSource1 = CreateTemplateSource(255);
        using var templateSource2 = CreateTemplateSource(128);
        using var template1 = new GaMat(templateSource1, false);
        using var template2 = new GaMat(templateSource2, false);
        var result = new TemplateMatchResult(0.9, 0.1, new Point(2, 3), Point.Empty);
        var pool = new TemplateMatchCachePool();
        var otherPool = new TemplateMatchCachePool();

        pool.SetFrameIndex(10);
        otherPool.SetFrameIndex(10);
        pool.RegisterResult(TemplateMatchCachePool.MatchUsage.Banner, image, template1,
            TemplateMatchingType.CcoeffNormed, result);

        Assert.True(pool.TryGet(TemplateMatchCachePool.MatchUsage.Banner, image, template1,
            TemplateMatchingType.CcoeffNormed, out var cached));
        Assert.Equal(result, cached);
        Assert.False(pool.TryGet(TemplateMatchCachePool.MatchUsage.Banner, image, template2,
            TemplateMatchingType.CcoeffNormed, out _));
        Assert.False(pool.TryGet(TemplateMatchCachePool.MatchUsage.Banner, image, template1,
            TemplateMatchingType.CcorrNormed, out _));
        Assert.False(otherPool.TryGet(TemplateMatchCachePool.MatchUsage.Banner, image, template1,
            TemplateMatchingType.CcoeffNormed, out _));
    }

    private static Mat CreateTemplateSource(byte gray)
    {
        var source = new Mat(8, 8, DepthType.Cv8U, 4);
        source.SetTo(new MCvScalar(gray, gray, gray, 255));
        return source;
    }
}

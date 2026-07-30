using System.Drawing;
using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;

namespace SekaiToolsCore.Match.TemplateMatcher;

public static class TemplateMatcher
{
    private const int SearchDownscaleDivisor = 2;
    private const int MinTemplateDimAfterScale = 8;
    private const int RefinementMargin = 4;
    private static readonly Inter SearchInterpolation = Inter.Area;

    public static TemplateMatchResult Match(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchCachePool cachePool,
        TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.Misc,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        [CallerMemberName] string memberName = "")
    {
        if (cachePool.TryGet(usage, frame.Gray, region, tmp, matchingType, out var cachedResult))
            return cachedResult;

        var res = new TemplateMatchResult(double.NegativeInfinity, double.PositiveInfinity,
            Point.Empty, Point.Empty);
        foreach (var scale in cachePool.GetCandidateScales(usage))
        {
            var candidate = MatchNoCache(frame, region, tmp, scale, matchingType, memberName);
            if (candidate.MaxVal > res.MaxVal)
                res = candidate;
        }

        cachePool.ObserveScale(usage, res.Scale, res.MaxVal);
        cachePool.RegisterResult(usage, frame.Gray, region, tmp, matchingType, res);

        return res;
    }

    private static TemplateMatchResult MatchNoCache(FrameMatchContext frame, Rectangle region, GaMat tmp,
        double scale,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        var templateLayer = tmp.GetScaledLayer(scale, 1);
        if (templateLayer.Size.Width > region.Width || templateLayer.Size.Height > region.Height)
            return new TemplateMatchResult(double.NegativeInfinity, double.PositiveInfinity,
                Point.Empty, Point.Empty) { Scale = scale };

        if (templateLayer.Size.Width / SearchDownscaleDivisor >= MinTemplateDimAfterScale
            && templateLayer.Size.Height / SearchDownscaleDivisor >= MinTemplateDimAfterScale)
            return MatchNoCacheScaled(frame, region, tmp, templateLayer, scale, matchingType, memberName);

        using var image = frame.CreateGrayRoi(region);
        return MatchNoCacheFull(image, templateLayer, scale, matchingType, memberName);
    }

    private static TemplateMatchResult MatchNoCacheScaled(FrameMatchContext frame, Rectangle region, GaMat tmp,
        GaMatLayer fullTemplateLayer, double scale, TemplateMatchingType matchingType, string memberName)
    {
        var imgSmall = frame.GetScaledGrayRoi(region, SearchDownscaleDivisor, SearchInterpolation);
        var templateLayer = tmp.GetScaledLayer(scale, SearchDownscaleDivisor);

        using var matchResult = new Mat();
        CvInvoke.MatchTemplate(imgSmall, templateLayer.Gray, matchResult, matchingType, templateLayer.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        var coarseMaxLoc = new Point(
            maxLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            maxLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);
        var coarseMinLoc = new Point(
            minLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            minLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);

        var refinementRegion = new Rectangle(
            coarseMaxLoc.X - RefinementMargin,
            coarseMaxLoc.Y - RefinementMargin,
            fullTemplateLayer.Size.Width + RefinementMargin * 2,
            fullTemplateLayer.Size.Height + RefinementMargin * 2);
        refinementRegion = Rectangle.Intersect(refinementRegion, new Rectangle(Point.Empty, region.Size));

        if (refinementRegion.Width >= fullTemplateLayer.Size.Width &&
            refinementRegion.Height >= fullTemplateLayer.Size.Height)
        {
            var frameRefinementRegion = new Rectangle(
                region.X + refinementRegion.X,
                region.Y + refinementRegion.Y,
                refinementRegion.Width,
                refinementRegion.Height);
            using var refinementImage = frame.CreateGrayRoi(frameRefinementRegion);
            using var refinementResult = new Mat();
            CvInvoke.MatchTemplate(refinementImage, fullTemplateLayer.Gray, refinementResult, matchingType,
                fullTemplateLayer.Alpha);
            refinementResult.MatRemoveErrorInf();
            CvInvoke.MinMaxLoc(refinementResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
            maxLoc += new Size(refinementRegion.Location);
            minLoc += new Size(refinementRegion.Location);
        }
        else
        {
            maxLoc = coarseMaxLoc;
            minLoc = coarseMinLoc;
        }

        using var image = frame.CreateGrayRoi(region);
        ShowImg(image, fullTemplateLayer, maxVal, maxLoc, memberName);
        return new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc) { Scale = scale };
    }

    private static TemplateMatchResult MatchNoCacheFull(Mat img, GaMatLayer templateLayer, double scale,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        using var matchResult = new Mat();
        CvInvoke.MatchTemplate(img, templateLayer.Gray, matchResult, matchingType, templateLayer.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
        ShowImg(img, templateLayer, maxVal, maxLoc, memberName);
        return new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc) { Scale = scale };
    }

    private static void ShowImg(Mat img, GaMatLayer tmp, double maxVal, Point maxLoc, string memberName)
    {
        var areas = Environment.GetEnvironmentVariable("DebugShowImg") ?? "";
        if (!areas.Contains(memberName))
            return;

        using var show = img.Clone()!;
        CvInvoke.PutText(show, $"MaxVal: {maxVal:0.00}", maxLoc with { Y = maxLoc.Y - 5 },
            FontFace.HersheySimplex, 0.4, new MCvScalar(255));
        CvInvoke.Rectangle(show, new Rectangle(maxLoc, tmp.Size), new MCvScalar(255), 2);


        using var tempGray = tmp.Gray.Clone();
        using var tempAlpha = tmp.Alpha.Clone();
        using var temp = new Mat(tempAlpha.Rows + tempGray.Rows, tempGray.Cols, tempGray.Depth,
            tempGray.NumberOfChannels);
        CvInvoke.VConcat(new VectorOfMat(tempGray, tempAlpha), temp);
        if (temp.Height > show.Height)
        {
            using var emptyMat = new Mat(temp.Rows - show.Rows, show.Cols, show.Depth, show.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, show), show);
        }
        else if (temp.Height < show.Height)
        {
            using var emptyMat = new Mat(show.Rows - temp.Rows, temp.Cols, temp.Depth, temp.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, temp), temp);
        }

        CvInvoke.HConcat(new VectorOfMat(show, temp), show);


        // var emptyMat = new Mat(show.Rows - tempGray.Rows, tempGray.Cols, tempGray.Depth, tempGray.NumberOfChannels);
        // emptyMat.SetTo(new MCvScalar(0));
        // CvInvoke.VConcat(new VectorOfMat(emptyMat, tempGray), tempGray);
        // CvInvoke.HConcat(new VectorOfMat(show, tempGray), show);
        // tempGray.Dispose();


        CvInvoke.Imshow(memberName, show);
        CvInvoke.WaitKey(Environment.GetEnvironmentVariable("DebugImgWait") == "true" ? 0 : 1);
    }
}

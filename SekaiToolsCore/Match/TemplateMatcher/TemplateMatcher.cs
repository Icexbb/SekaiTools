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
    private static readonly Inter SearchInterpolation = Inter.Area;

    public static TemplateMatchResult Match(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchCachePool cachePool,
        TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.Misc,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        [CallerMemberName] string memberName = "")
    {
        if (cachePool.TryGet(usage, frame.Gray, region, tmp, matchingType, out var cachedResult))
            return cachedResult;

        var res = MatchNoCache(frame, region, tmp, matchingType, memberName);
        cachePool.RegisterResult(usage, frame.Gray, region, tmp, matchingType, res);

        return res;
    }

    private static TemplateMatchResult MatchNoCache(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        if (tmp.Size.Width / SearchDownscaleDivisor >= MinTemplateDimAfterScale
            && tmp.Size.Height / SearchDownscaleDivisor >= MinTemplateDimAfterScale)
            return MatchNoCacheScaled(frame, region, tmp, matchingType, memberName);

        using var image = frame.CreateGrayRoi(region);
        return MatchNoCacheFull(image, tmp, matchingType, memberName);
    }

    private static TemplateMatchResult MatchNoCacheScaled(FrameMatchContext frame, Rectangle region, GaMat tmp,
        TemplateMatchingType matchingType, string memberName)
    {
        var imgSmall = frame.GetScaledGrayRoi(region, SearchDownscaleDivisor, SearchInterpolation);
        var templateLayer = tmp.GetScaledLayer(SearchDownscaleDivisor);

        using var matchResult = new Mat();
        CvInvoke.MatchTemplate(imgSmall, templateLayer.Gray, matchResult, matchingType, templateLayer.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        maxLoc = new Point(
            maxLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            maxLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);
        minLoc = new Point(
            minLoc.X * SearchDownscaleDivisor + SearchDownscaleDivisor / 2,
            minLoc.Y * SearchDownscaleDivisor + SearchDownscaleDivisor / 2);

        using var image = frame.CreateGrayRoi(region);
        ShowImg(image, tmp, maxVal, maxLoc, memberName);
        return new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc);
    }

    private static TemplateMatchResult MatchNoCacheFull(Mat img, GaMat tmp,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        using var matchResult = new Mat();
        CvInvoke.MatchTemplate(img, tmp.Gray, matchResult, matchingType, tmp.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
        ShowImg(img, tmp, maxVal, maxLoc, memberName);
        return new TemplateMatchResult(maxVal, minVal, maxLoc, minLoc);
    }

    private static void ShowImg(Mat img, GaMat tmp, double maxVal, Point maxLoc, string memberName)
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

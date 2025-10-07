using System.Drawing;
using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;

namespace SekaiToolsCore.Process;

public static class TemplateMatcher
{
    public static MatchResult Match(Mat imgOriginal, GaMat tmp,
        TemplateMatchCachePool.MatchUsage usage = TemplateMatchCachePool.MatchUsage.Misc,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        [CallerMemberName] string memberName = "")
    {
        var img = new Mat();
        if (imgOriginal.NumberOfChannels == 3)
        {
            CvInvoke.CvtColor(imgOriginal, img, ColorConversion.Bgr2Gray);
        }
        else
        {
            img = imgOriginal;
        }

        var pool = TemplateMatchCachePool.GetPool(usage);
        if (pool.Query(img))
        {
            return pool.prevResult;
        }

        var res = MatchNoCache(img, tmp, matchingType, memberName);
        pool.RegisterResult(img, res);

        return res;
    }

    private static MatchResult MatchNoCache(Mat img, GaMat tmp,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        string memberName = "")
    {
        var matchResult = new Mat();
        CvInvoke.MatchTemplate(img, tmp.Gray, matchResult, matchingType, tmp.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
        ShowImg(img, tmp, maxVal, maxLoc, memberName);
        matchResult.Dispose();
        return new MatchResult(maxVal, minVal, maxLoc, minLoc);
    }

    private static void ShowImg(Mat img, GaMat tmp, double maxVal, Point maxLoc, string memberName)
    {
        var areas = Environment.GetEnvironmentVariable("DebugShowImg") ?? "";
        if (!areas.Contains(memberName))
            return;

        var show = img.Clone()!;
        CvInvoke.PutText(show, $"MaxVal: {maxVal:0.00}", maxLoc with { Y = maxLoc.Y - 5 },
            FontFace.HersheySimplex, 0.4, new MCvScalar(255));
        CvInvoke.Rectangle(show, new Rectangle(maxLoc, tmp.Size), new MCvScalar(255), 2);


        var tempGray = tmp.Gray.Clone();
        var tempAlpha = tmp.Alpha.Clone();
        var temp = new Mat(tempAlpha.Rows + tempGray.Rows, tempGray.Cols, tempGray.Depth, tempGray.NumberOfChannels);
        CvInvoke.VConcat(new VectorOfMat(tempGray, tempAlpha), temp);
        if (temp.Height > show.Height)
        {
            var emptyMat = new Mat(temp.Rows - show.Rows, show.Cols, show.Depth, show.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, show), show);
        }
        else if (temp.Height < show.Height)
        {
            var emptyMat = new Mat(show.Rows - temp.Rows, temp.Cols, temp.Depth, temp.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, temp), temp);
        }

        CvInvoke.HConcat(new VectorOfMat(show, temp), show);
        tempGray.Dispose();
        tempAlpha.Dispose();
        temp.Dispose();

        // var emptyMat = new Mat(show.Rows - tempGray.Rows, tempGray.Cols, tempGray.Depth, tempGray.NumberOfChannels);
        // emptyMat.SetTo(new MCvScalar(0));
        // CvInvoke.VConcat(new VectorOfMat(emptyMat, tempGray), tempGray);
        // CvInvoke.HConcat(new VectorOfMat(show, tempGray), show);
        // tempGray.Dispose();


        CvInvoke.Imshow(memberName, show);
        CvInvoke.WaitKey(Environment.GetEnvironmentVariable("DebugImgWait") == "true" ? 0 : 1);
    }
}
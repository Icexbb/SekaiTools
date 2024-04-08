using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace SekaiToolsCore.Process;

public static class Matcher
{
    public static MatchResult MatchTemplate(Mat img, GaMat tmp,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed,
        [System.Runtime.CompilerServices.CallerMemberName]
        string memberName = "")
    {
        var matchResult = new Mat();
        CvInvoke.MatchTemplate(img, tmp.Gray, matchResult, matchingType, mask: tmp.Alpha);
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
        var temp = tmp.Gray.Clone();
        var emptyMat = new Mat(show.Rows - temp.Rows, temp.Cols, temp.Depth, temp.NumberOfChannels);
        emptyMat.SetTo(new MCvScalar(0));
        CvInvoke.VConcat(new VectorOfMat(emptyMat, temp), temp);
        CvInvoke.HConcat(new VectorOfMat(show, temp), show);
        temp.Dispose();

        CvInvoke.PutText(show, $"MaxVal: {maxVal:0.00}", maxLoc with { Y = maxLoc.Y - 5 },
            FontFace.HersheySimplex, 0.4, new MCvScalar(255));
        CvInvoke.Rectangle(show, new Rectangle(maxLoc, tmp.Size), new MCvScalar(255), 2);

        CvInvoke.Imshow(memberName, show);
        CvInvoke.WaitKey(Environment.GetEnvironmentVariable("DebugImgWait") == "true" ? 0 : 1);
    }
}
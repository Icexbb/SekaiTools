using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class TemplateMatchCachePool
{
    public enum MatchUsage
    {
        ContentStartSign = 0,
        Banner = 1,
        DialogNameTag = 2,
        DialogContent1 = 3,
        DialogContent2 = 4,
        DialogContent3 = 5,
        Marker = 6,
        Misc = 7
    }

    private static List<TemplateMatchCachePool>? _globalPool;
    public Mat diffMat;

    public Mat? prevImg;
    public TemplateMatchResult prevResult;

    public TemplateMatchCachePool()
    {
        diffMat = new Mat();
    }

    private static List<TemplateMatchCachePool> GlobalPool
    {
        get
        {
            if (_globalPool != null) return _globalPool;
            const int len = (int)MatchUsage.Misc + 1;
            _globalPool = new List<TemplateMatchCachePool>(len);

            for (var i = 0; i < len; i++) _globalPool.Add(new TemplateMatchCachePool());

            return _globalPool;
        }
    }

    public static TemplateMatchCachePool GetPool(MatchUsage usage)
    {
        return GlobalPool[(int)usage];
    }

    public static void NextDialog()
    {
        GlobalPool[(int)MatchUsage.DialogNameTag].Reset();
        GlobalPool[(int)MatchUsage.DialogContent1].Reset();
        GlobalPool[(int)MatchUsage.DialogContent2].Reset();
        GlobalPool[(int)MatchUsage.DialogContent3].Reset();
    }

    public void RegisterResult(Mat img, TemplateMatchResult result)
    {
        prevImg = img;
        prevResult = result;
    }

    public bool Query(Mat img)
    {
        if (img == null || prevImg == null) return false;

        // treat two empty mat as identical as well
        if (img.IsEmpty && prevImg.IsEmpty) return true;
        // if dimensionality of two mat is not identical, these two mat is not identical
        if (img.Cols != prevImg.Cols || img.Rows != prevImg.Rows || img.Dims != prevImg.Dims) return false;

        CvInvoke.Compare(img, prevImg, diffMat, CmpType.NotEqual);
        var diffPx = CvInvoke.CountNonZero(diffMat);

        if (diffPx > 0) return false;

        return true;
    }

    private void Reset()
    {
        prevImg = null;
    }
}
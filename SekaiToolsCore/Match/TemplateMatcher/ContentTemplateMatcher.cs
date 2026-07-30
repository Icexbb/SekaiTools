using System.Drawing;
using Emgu.CV;
using SekaiToolsBase;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class ContentTemplateMatcher(
    TemplateManager templateManager,
    TemplateMatchCachePool cachePool,
    Config config) : IDisposable
{
    private GaMat Template { get; } = new(templateManager.GetMenuSign(), false);

    private double Threshold { get; } = config.MatchingThreshold.DialogContentNormal;

    public bool Finished { get; private set; }

    public void Dispose()
    {
        Template.Dispose();
    }

    private bool MatchContentStartSign(Mat mat, int frameIndex = -1)
    {
        var width = Template.Size.Width * 3;
        var height = Template.Size.Height * 2;
        var roi = new Rectangle(mat.Width - width, 0, width, height);
        roi.Limit(new Rectangle(Point.Empty, mat.Size));
        if (roi.Width < Template.Size.Width || roi.Height < Template.Size.Height)
            return false;

        using var frameCropped = new Mat(mat, roi);
        var result = TemplateMatcher.Match(frameCropped, Template, cachePool,
            TemplateMatchCachePool.MatchUsage.ContentStartSign);

        if (frameIndex != -1)
            Logger.Log(
                $"{nameof(ContentTemplateMatcher)} Frame {frameIndex} Match Content Start Sign Result: {result.MaxVal}",
                ExtLogLevel.Debug
            );

        return result.MaxVal > Threshold;
    }

    public void Process(Mat mat)
    {
        if (MatchContentStartSign(mat)) Finished = true;
    }

    public void ForceFinish()
    {
        Finished = true;
    }
}

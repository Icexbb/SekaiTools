using System.Drawing;
using Emgu.CV;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiToolsBase;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class ContentTemplateMatcher(TemplateManager templateManager, Config config)
{
    private GaMat Template { get; } = new(templateManager.GetMenuSign(), false);

    private double Threshold { get; } = config.MatchingThreshold.DialogContentNormal;

    public bool Finished { get; private set; }

    private bool MatchContentStartSign(Mat mat, int frameIndex = -1)
    {
        var width = Template.Size.Width * 3;
        var height = Template.Size.Height * 2;
        var roi = new Rectangle(mat.Width - width, 0, width, height);
        roi.Extend(0.1);

        var frameCropped = new Mat(mat, roi);
        var result = TemplateMatcher.Match(frameCropped, Template, TemplateMatchCachePool.MatchUsage.ContentStartSign);

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
}
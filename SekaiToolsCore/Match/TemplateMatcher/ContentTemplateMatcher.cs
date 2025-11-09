using System.Drawing;
using Emgu.CV;
using SekaiToolsBase;
using SekaiToolsCore.Process;
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
        var roi = new Rectangle(
            mat.Width - Template.Size.Width * 3, 0,
            Template.Size.Width * 3,
            Template.Size.Height * 2
        );
        roi.Extend(0.1);

        var frameCropped = new Mat(mat, roi);
        var result = TemplateMatcher.Match(frameCropped, Template, TemplateMatchCachePool.MatchUsage.ContentStartSign);

        if (frameIndex != -1)
            Logger.Log(
                $"{nameof(ContentTemplateMatcher)} Frame {frameIndex} Match Content Start Sign Result: {result.MaxVal}"
            );


        return result.MaxVal > Threshold;
    }

    public void Process(Mat mat)
    {
        if (MatchContentStartSign(mat)) Finished = true;
    }
}
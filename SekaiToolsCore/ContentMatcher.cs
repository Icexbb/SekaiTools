using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process;

namespace SekaiToolsCore;

public class ContentMatcher(TemplateManager templateManager, Config config)
{
    private GaMat Template { get; set; } = new GaMat(templateManager.GetMenuSign(), false);

    private double Threshold { get; set; } = config.MatchingThreshold.Normal;

    private bool MatchContentStartSign(Mat mat)
    {
        var roi = new Rectangle(
            mat.Width - Template.Size.Width * 3, 0,
            Template.Size.Width * 3,
            Template.Size.Height * 2
        );
        roi.Extend(0.1);

        var frameCropped = new Mat(mat, roi);
        var result = Matcher.MatchTemplate(frameCropped, Template);
        return result.MaxVal > Threshold;
    }

    public void Process(Mat mat)
    {
        if (MatchContentStartSign(mat)) Finished = true;
    }

    public bool Finished { get; private set; }
}
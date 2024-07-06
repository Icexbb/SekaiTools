using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process;

namespace SekaiToolsCore;

public class ContentMatcher(TemplateManager templateManager, Config config)
{
    public void Process(Mat mat)
    {
        var menuSign = new GaMat(templateManager.GetMenuSign(), false);
        var startThreshold = config.MatchingThreshold.Normal;
        var roi = new Rectangle(
            mat.Width - menuSign.Size.Width * 2, 0, menuSign.Size.Width * 2, menuSign.Size.Height * 2
        );
        roi.Extend(0.1);
        var frameCropped = new Mat(mat, roi);
        var result = Matcher.MatchTemplate(frameCropped, menuSign);
        Finished = result.MaxVal > startThreshold;
    }

    public bool Finished { get; set; }
}
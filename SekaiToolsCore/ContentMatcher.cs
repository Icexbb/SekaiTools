using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process;

namespace SekaiToolsCore;

public class ContentMatcher(TemplateManager templateManager)
{
    public void Process(Mat mat)
    {
        var menuSign = new GaMat(templateManager.GetMenuSign(), false);
        const double startThreshold = 0.85;
        var roi = new Rectangle(
            mat.Width - menuSign.Size.Width * 2, 0, menuSign.Size.Width * 2, menuSign.Size.Height * 2
        );
        roi.Extend(0.1);
        var frameCropped = new Mat(mat, roi);
        var result = Matcher.MatchTemplate(frameCropped, menuSign);
        IsFinished = result.MaxVal > startThreshold;
    }

    public bool IsFinished { get; set; }
}
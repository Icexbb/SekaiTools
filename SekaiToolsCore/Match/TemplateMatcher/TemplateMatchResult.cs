using System.Drawing;

namespace SekaiToolsCore.Match.TemplateMatcher;

public struct TemplateMatchResult(double maxVal, double minVal, Point maxLoc, Point minLoc)
{
    public double MaxVal { get; set; } = maxVal;
    public double MinVal { get; set; } = minVal;

    public Point MaxLoc { get; set; } = maxLoc;
    public Point MinLoc { get; set; } = minLoc;
}
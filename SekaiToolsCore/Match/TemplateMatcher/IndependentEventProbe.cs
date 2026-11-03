using System.Drawing;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Process.Performance;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal static class IndependentEventProbe
{
    public static bool IsDue(int frameIndex, double fps)
    {
        return frameIndex % Math.Max(2, (int)Math.Ceiling(fps * 0.5)) == 0;
    }

    public static bool Confirm(FrameMatchContext frame, GaMat template, Rectangle region,
        TemplateMatchingMetrics metrics, double configuredThreshold)
    {
        region = Rectangle.Intersect(region, new Rectangle(Point.Empty, frame.Size));
        if (region.Width < template.Size.Width || region.Height < template.Size.Height) return false;
        // Probes cannot calibrate the normal matcher, lower thresholds or advance a target.
        var evidence = new TemplateMatchCachePool { Metrics = metrics };
        return TemplateMatcher.Match(frame, region, template, evidence)
            .IsMatch(Math.Max(0.90, configuredThreshold));
    }
}
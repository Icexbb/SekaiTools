using System.Drawing;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal static class MatchedFrameRecorder
{
    public static void Record(DialogBaseFrameSet set, int selectedIndex, Point selectedPoint,
        int currentIndex, Point currentPoint)
    {
        set.Add(selectedIndex, selectedPoint);
        if (selectedIndex < currentIndex) set.Add(currentIndex, currentPoint);
    }

    public static void Record(MarkerBaseFrameSet set, int selectedIndex, Point selectedPoint,
        int currentIndex, Point currentPoint)
    {
        set.Add(selectedIndex, selectedPoint);
        if (selectedIndex < currentIndex) set.Add(currentIndex, currentPoint);
    }

    public static void Record(BannerBaseFrameSet set, int selectedIndex, int currentIndex)
    {
        set.Add(selectedIndex);
        if (selectedIndex < currentIndex) set.Add(currentIndex);
    }
}

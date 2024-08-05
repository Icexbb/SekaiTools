using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class ModdedTags
{
    public static BaselineObliquity BaselineObliquity(int angle)
    {
        return new BaselineObliquity(angle);
    }

    public static XBlur XBlur(int value)
    {
        return new XBlur(value);
    }

    public static YBlur YBlur(int value)
    {
        return new YBlur(value);
    }

    public static BoundariesDeforming BoundariesDeforming(int value)
    {
        return new BoundariesDeforming(value);
    }

    public static BoundariesDeformingX BoundariesDeformingX(int value)
    {
        return new BoundariesDeformingX(value);
    }

    public static BoundariesDeformingY BoundariesDeformingY(int value)
    {
        return new BoundariesDeformingY(value);
    }

    public static BoundariesDeformingZ BoundariesDeformingZ(int value)
    {
        return new BoundariesDeformingZ(value);
    }

    public static Distortion Distortion(int rightTopX, int rightTopY, int rightBottomX, int rightBottomY,
        int leftBottomX, int leftBottomY)
    {
        return new Distortion(rightTopX, rightTopY, rightBottomX, rightBottomY, leftBottomX, leftBottomY);
    }

    public static Distortion Distortion(Point rightTop, Point rightBottom, Point leftBottom)
    {
        return new Distortion(rightTop, rightBottom, leftBottom);
    }

    public static FontScale FontScale(int scale = 100)
    {
        return new FontScale(scale);
    }

    public static PrimaryGradientAlpha PrimaryGradientAlpha(SubStationAlpha.Alpha leftTop,
        SubStationAlpha.Alpha rightTop,
        SubStationAlpha.Alpha leftBottom, SubStationAlpha.Alpha rightBottom)
    {
        return new PrimaryGradientAlpha(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static SecondaryGradientAlpha SecondaryGradientAlpha(SubStationAlpha.Alpha leftTop,
        SubStationAlpha.Alpha rightTop,
        SubStationAlpha.Alpha leftBottom, SubStationAlpha.Alpha rightBottom)
    {
        return new SecondaryGradientAlpha(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static OutlineGradientAlpha OutlineGradientAlpha(SubStationAlpha.Alpha leftTop,
        SubStationAlpha.Alpha rightTop,
        SubStationAlpha.Alpha leftBottom, SubStationAlpha.Alpha rightBottom)
    {
        return new OutlineGradientAlpha(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static ShadowGradientAlpha ShadowGradientAlpha(SubStationAlpha.Alpha leftTop, SubStationAlpha.Alpha rightTop,
        SubStationAlpha.Alpha leftBottom, SubStationAlpha.Alpha rightBottom)
    {
        return new ShadowGradientAlpha(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static PrimaryGradientsColor PrimaryGradientsColor(SubStationAlpha.Color leftTop,
        SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom)
    {
        return new PrimaryGradientsColor(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static SecondaryGradientsColor SecondaryGradientsColor(SubStationAlpha.Color leftTop,
        SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom)
    {
        return new SecondaryGradientsColor(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static OutlineGradientsColor OutlineGradientsColor(SubStationAlpha.Color leftTop,
        SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom)
    {
        return new OutlineGradientsColor(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static ShadowGradientsColor ShadowGradientsColor(SubStationAlpha.Color leftTop,
        SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom)
    {
        return new ShadowGradientsColor(leftTop, rightTop, leftBottom, rightBottom);
    }

    public static PrimaryImage PrimaryImage(string path, int x = 0, int y = 0)
    {
        return new PrimaryImage(path, x, y);
    }

    public static PrimaryImage PrimaryImage(string path, Point offset)
    {
        return new PrimaryImage(path, offset.X, offset.Y);
    }

    public static SecondaryImage SecondaryImage(string path, int x = 0, int y = 0)
    {
        return new SecondaryImage(path, x, y);
    }

    public static SecondaryImage SecondaryImage(string path, Point offset)
    {
        return new SecondaryImage(path, offset.X, offset.Y);
    }

    public static OutlineImage OutlineImage(string path, int x = 0, int y = 0)
    {
        return new OutlineImage(path, x, y);
    }

    public static OutlineImage OutlineImage(string path, Point offset)
    {
        return new OutlineImage(path, offset.X, offset.Y);
    }

    public static ShadowImage ShadowImage(string path, int x = 0, int y = 0)
    {
        return new ShadowImage(path, x, y);
    }

    public static ShadowImage ShadowImage(string path, Point offset)
    {
        return new ShadowImage(path, offset.X, offset.Y);
    }

    public static Jitter Jitter(int left, int right, int up, int down, int period, int? seed = null)
    {
        return new Jitter(left, right, up, down, period, seed);
    }

    public static LeadingHorizontal LeadingHorizontal(int value)
    {
        return new LeadingHorizontal(value);
    }

    public static LeadingVertical LeadingVertical(int value)
    {
        return new LeadingVertical(value);
    }

    public static MovableVectorClip MovableVectorClip(int x1, int y1)
    {
        return new MovableVectorClip(x1, y1);
    }

    public static MovableVectorClip MovableVectorClip(Point point)
    {
        return new MovableVectorClip(point.X, point.Y);
    }

    public static MovableVectorClip MovableVectorClip(int x1, int y1, int x2, int y2,
        int? time1 = null, int? time2 = null)
    {
        return new MovableVectorClip(x1, y1, x2, y2, time1, time2);
    }

    public static MovableVectorClip MovableVectorClip(Point point1, Point point2,
        int? time1 = null, int? time2 = null)
    {
        return new MovableVectorClip(point1.X, point1.Y, point2.X, point2.Y, time1, time2);
    }

    public static OrthogonalProjection OrthogonalProjection(bool value)
    {
        return new OrthogonalProjection(value);
    }

    public static PolarMove PolarMove(int x1, int y1, int x2, int y2, int angle1, int angle2, int radius1, int radius2,
        int time1 = 0, int time2 = 0)
    {
        return new PolarMove(x1, y1, x2, y2, angle1, angle2, radius1, radius2, time1, time2);
    }

    public static SplineMove SplineMove(Point point1, Point point2, Point point3, int time1 = 0, int time2 = 0)
    {
        return new SplineMove(point1, point2, point3, time1, time2);
    }

    public static SplineMove SplineMove(Point point1, Point point2, Point point3, Point point4,
        int time1 = 0, int time2 = 0)
    {
        return new SplineMove(point1, point2, point3, point4, time1, time2);
    }

    public static Z Z(int value)
    {
        return new Z(value);
    }
}
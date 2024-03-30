using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class ModdedTags
{
    public static BaselineObliquity BaselineObliquity(int angle) => new(angle);

    public static XBlur XBlur(int value) => new(value);

    public static YBlur YBlur(int value) => new(value);

    public static BoundariesDeforming BoundariesDeforming(int value) => new(value);

    public static BoundariesDeformingX BoundariesDeformingX(int value) => new(value);

    public static BoundariesDeformingY BoundariesDeformingY(int value) => new(value);

    public static BoundariesDeformingZ BoundariesDeformingZ(int value) => new(value);

    public static Distortion Distortion(int rightTopX, int rightTopY, int rightBottomX, int rightBottomY,
        int leftBottomX, int leftBottomY) =>
        new(rightTopX, rightTopY, rightBottomX, rightBottomY, leftBottomX, leftBottomY);

    public static Distortion Distortion(Point rightTop, Point rightBottom, Point leftBottom) =>
        new(rightTop, rightBottom, leftBottom);

    public static FontScale FontScale(int scale = 100) => new(scale);

    public static PrimaryGradientAlpha PrimaryGradientAlpha(SubtitleAlpha leftTop, SubtitleAlpha rightTop,
        SubtitleAlpha leftBottom, SubtitleAlpha rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static SecondaryGradientAlpha SecondaryGradientAlpha(SubtitleAlpha leftTop, SubtitleAlpha rightTop,
        SubtitleAlpha leftBottom, SubtitleAlpha rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static OutlineGradientAlpha OutlineGradientAlpha(SubtitleAlpha leftTop, SubtitleAlpha rightTop,
        SubtitleAlpha leftBottom, SubtitleAlpha rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static ShadowGradientAlpha ShadowGradientAlpha(SubtitleAlpha leftTop, SubtitleAlpha rightTop,
        SubtitleAlpha leftBottom, SubtitleAlpha rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static PrimaryGradientsColor PrimaryGradientsColor(SubStationAlpha.Color leftTop, SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static SecondaryGradientsColor SecondaryGradientsColor(SubStationAlpha.Color leftTop, SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static OutlineGradientsColor OutlineGradientsColor(SubStationAlpha.Color leftTop, SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static ShadowGradientsColor ShadowGradientsColor(SubStationAlpha.Color leftTop, SubStationAlpha.Color rightTop,
        SubStationAlpha.Color leftBottom, SubStationAlpha.Color rightBottom) =>
        new(leftTop, rightTop, leftBottom, rightBottom);

    public static PrimaryImage PrimaryImage(string path, int x = 0, int y = 0) => new(path, x, y);
    public static PrimaryImage PrimaryImage(string path, Point offset) => new(path, offset.X, offset.Y);

    public static SecondaryImage SecondaryImage(string path, int x = 0, int y = 0) => new(path, x, y);
    public static SecondaryImage SecondaryImage(string path, Point offset) => new(path, offset.X, offset.Y);

    public static OutlineImage OutlineImage(string path, int x = 0, int y = 0) => new(path, x, y);
    public static OutlineImage OutlineImage(string path, Point offset) => new(path, offset.X, offset.Y);

    public static ShadowImage ShadowImage(string path, int x = 0, int y = 0) => new(path, x, y);
    public static ShadowImage ShadowImage(string path, Point offset) => new(path, offset.X, offset.Y);

    public static Jitter Jitter(int left, int right, int up, int down, int period, int? seed = null) =>
        new(left, right, up, down, period, seed);

    public static LeadingHorizontal LeadingHorizontal(int value) => new(value);

    public static LeadingVertical LeadingVertical(int value) => new(value);

    public static MovableVectorClip MovableVectorClip(int x1, int y1) => new(x1, y1);
    public static MovableVectorClip MovableVectorClip(Point point) => new(point.X, point.Y);

    public static MovableVectorClip MovableVectorClip(int x1, int y1, int x2, int y2,
        int? time1 = null, int? time2 = null) => new(x1, y1, x2, y2, time1, time2);

    public static MovableVectorClip MovableVectorClip(Point point1, Point point2,
        int? time1 = null, int? time2 = null) => new(point1.X, point1.Y, point2.X, point2.Y, time1, time2);

    public static OrthogonalProjection OrthogonalProjection(bool value) => new(value);

    public static PolarMove PolarMove(int x1, int y1, int x2, int y2, int angle1, int angle2, int radius1, int radius2,
        int time1 = 0, int time2 = 0) => new(x1, y1, x2, y2, angle1, angle2, radius1, radius2, time1, time2);

    public static SplineMove SplineMove(Point point1, Point point2, Point point3, int time1 = 0, int time2 = 0) =>
        new(point1, point2, point3, time1, time2);

    public static SplineMove SplineMove(Point point1, Point point2, Point point3, Point point4,
        int time1 = 0, int time2 = 0) => new(point1, point2, point3, point4, time1, time2);

    public static Z Z(int value) => new(value);
}
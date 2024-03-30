using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class Distortion(
    int rightTopX,
    int rightTopY,
    int rightBottomX,
    int rightBottomY,
    int leftBottomX,
    int leftBottomY)
    : Tag, INestableTag
{
    public override string Name => "distort";
    public int RightTopX { get; set; } = rightTopX;
    public int RightTopY { get; set; } = rightTopY;
    public int RightBottomX { get; set; } = rightBottomX;
    public int RightBottomY { get; set; } = rightBottomY;
    public int LeftBottomX { get; set; } = leftBottomX;
    public int LeftBottomY { get; set; } = leftBottomY;

    public Distortion(Point rightTop, Point rightBottom, Point leftBottom)
        : this(rightTop.X, rightTop.Y, rightBottom.X, rightBottom.Y, leftBottom.X, leftBottom.Y)
    {
    }

    public override string ToString() =>
        $"\\{Name}({RightTopX},{RightTopY},{RightBottomX},{RightBottomY},{LeftBottomX},{LeftBottomY})";
}
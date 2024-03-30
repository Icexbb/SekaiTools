using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public class RotateCenter(Point point) : Tag
{
    public override string Name => "org";
    public Point Point { get; } = point;

    public RotateCenter(int x, int y) : this(new Point(x, y))
    {
    }

    public override string ToString() => $"\\{Name}({Point.X},{Point.Y})";
}
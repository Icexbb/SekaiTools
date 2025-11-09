using System.Drawing;

namespace SekaiToolsBase.SubStationAlpha.Tag;

public class RotateCenter(Point point) : Tag
{
    public RotateCenter(int x, int y) : this(new Point(x, y))
    {
    }

    public override string Name => "org";
    public Point Point { get; } = point;

    public override string ToString()
    {
        return $"\\{Name}({Point.X},{Point.Y})";
    }
}
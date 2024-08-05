using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Position(Point point) : Tag
{
    public Position(int x, int y) : this(new Point(x, y))
    {
    }

    public override string Name => "pos";
    public Point Point { get; } = point;

    public override string ToString()
    {
        return $"\\{Name}({Point.X},{Point.Y})";
    }
}
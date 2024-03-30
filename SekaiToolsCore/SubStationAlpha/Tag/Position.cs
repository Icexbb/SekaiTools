using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Position(Point point) : Tag
{
    public override string Name => "pos";
    public Point Point { get; } = point;

    public Position(int x, int y) : this(new Point(x, y))
    {
    }

    public override string ToString() => $"\\{Name}({Point.X},{Point.Y})";
}
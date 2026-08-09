using System.Drawing;

namespace SekaiToolsBase.SubStationAlpha.Tag;

public class Move(Point from, Point to, int start = 0, int end = 0) : Tag
{
    public Move(int fromX, int fromY, int toX, int toY, int start = 0, int end = 0) : this(new Point(fromX, fromY),
        new Point(toX, toY), start, end)
    {
    }

    public Point From { get; } = from;
    public Point To { get; } = to;
    public int Start { get; } = start;
    public int End { get; } = end;
    public override string Name => "move";

    public override string ToString()
    {
        if (Start == 0 && End == 0)
            return $"\\{Name}({From.X},{From.Y},{To.X},{To.Y})";
        return $"\\{Name}({From.X},{From.Y},{To.X},{To.Y},{Start},{End})";
    }
}
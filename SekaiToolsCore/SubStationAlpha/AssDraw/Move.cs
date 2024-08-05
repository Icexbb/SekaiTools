using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.AssDraw;

public class AssDrawMove(AssDrawPoint p) : AssDrawPart("m")
{
    public AssDrawMove(int x, int y) : this(new AssDrawPoint(x, y))
    {
    }

    public AssDrawMove(Point point) : this(point.X, point.Y)
    {
    }

    public override void Move(int x, int y)
    {
        p.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        p.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        p.Scale(ratio);
    }

    public override string ToString()
    {
        return $"{Type} {p.X} {p.Y}";
    }
}
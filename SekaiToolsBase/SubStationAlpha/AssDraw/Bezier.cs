using System.Drawing;

namespace SekaiToolsBase.SubStationAlpha.AssDraw;

public class Bezier(AssDrawPoint pA, AssDrawPoint pB, AssDrawPoint pC)
    : AssDrawPart("b")
{
    public Bezier(int x1, int y1, int x2, int y2, int x3, int y3) :
        this(new AssDrawPoint(x1, y1), new AssDrawPoint(x2, y2), new AssDrawPoint(x3, y3))
    {
    }

    public Bezier(Point pointA, Point pointB, Point pointC) :
        this(pointA.X, pointA.Y, pointB.X, pointB.Y, pointC.X, pointC.Y)
    {
    }

    public override void Move(int x, int y)
    {
        pA.Move(x, y);
        pB.Move(x, y);
        pC.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        pA.Scale(ratio);
        pB.Scale(ratio);
        pC.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        pA.Scale(ratio);
        pB.Scale(ratio);
        pC.Scale(ratio);
    }

    public override string ToString()
    {
        return $"{Type} {pA.X} {pA.Y} {pB.X} {pB.Y} {pC.X} {pC.Y}";
    }
}
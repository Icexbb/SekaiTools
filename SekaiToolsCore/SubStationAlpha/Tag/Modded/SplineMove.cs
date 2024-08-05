using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class SplineMove : Tag
{
    public SplineMove(Point point1, Point point2, Point point3, int time1 = 0, int time2 = 0)
    {
        Point1 = point1;
        Point2 = point2;
        Point3 = point3;
        Time1 = time1;
        Time2 = time2;
    }

    public SplineMove(Point point1, Point point2, Point point3, Point point4, int time1 = 0, int time2 = 0)
    {
        Point1 = point1;
        Point2 = point2;
        Point3 = point3;
        Point4 = point4;
        Time1 = time1;
        Time2 = time2;
    }

    public Point Point1 { get; set; }
    public Point Point2 { get; set; }
    public Point Point3 { get; set; }
    public Point? Point4 { get; set; }

    public int Time1 { get; set; }
    public int Time2 { get; set; }

    public override string Name => "moves";

    public override string ToString()
    {
        var time = Time1 == 0 && Time2 == 0 ? "" : $",{Time1},{Time2}";
        return Point4 == null
            ? $"\\{Name}3({Point1.X},{Point1.Y},{Point2.X},{Point2.Y},{Point3.X},{Point3.Y}{time})"
            : $"\\{Name}4({Point1.X},{Point1.Y},{Point2.X},{Point2.Y},{Point3.X},{Point3.Y},{Point4.Value.X},{Point4.Value.Y}{time})";
    }
}
namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class PolarMove(
    int x1,
    int y1,
    int x2,
    int y2,
    int angle1,
    int angle2,
    int radius1,
    int radius2,
    int time1 = 0,
    int time2 = 0)
    : Tag
{
    public int X1 { get; set; } = x1;
    public int Y1 { get; set; } = y1;
    public int X2 { get; set; } = x2;
    public int Y2 { get; set; } = y2;

    public int Angle1 { get; set; } = angle1;
    public int Angle2 { get; set; } = angle2;
    public int Radius1 { get; set; } = radius1;
    public int Radius2 { get; set; } = radius2;

    public int Time1 { get; set; } = time1;
    public int Time2 { get; set; } = time2;

    public override string Name => "mover";

    public override string ToString()
    {
        if (Time1 == 0 && Time2 == 0)
            return $"\\{Name}({X1},{Y1},{X2},{Y2},{Angle1},{Angle2},{Radius1},{Radius2})";
        return $"\\{Name}({X1},{Y1},{X2},{Y2},{Angle1},{Angle2},{Radius1},{Radius2},{Time1},{Time2})";
    }
}
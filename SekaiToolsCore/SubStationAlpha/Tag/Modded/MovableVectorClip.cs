namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class MovableVectorClip(int x1, int y1, int? x2 = null, int? y2 = null, int? time1 = null, int? time2 = null)
    : Tag
{
    public override string Name => "movevc";

    public int X1 { get; set; } = x1;
    public int Y1 { get; set; } = y1;
    public int? X2 { get; set; } = x2;
    public int? Y2 { get; set; } = y2;
    public int? Time1 { get; set; } = time1;
    public int? Time2 { get; set; } = time2;

    public override string ToString()
    {
        var result = $"\\{Name}({X1},{Y1}";
        if (X2.HasValue && Y2.HasValue)
        {
            result += $",{X2},{Y2}";
        }

        if (Time1.HasValue && Time2.HasValue)
        {
            result += $",{Time1},{Time2}";
        }

        return result + ")";
    }
}
namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class BaselineObliquity(int angle) : Tag, INestableTag
{
    public override string Name => "frs";
    public int Angle { get; set; } = angle;

    public override string ToString()
    {
        return $"\\{Name}{Angle}";
    }
}
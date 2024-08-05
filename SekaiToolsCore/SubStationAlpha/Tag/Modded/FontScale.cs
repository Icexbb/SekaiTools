namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class FontScale(int scale = 100) : Tag, INestableTag
{
    public override string Name => "fsc";
    public int Scale { get; } = scale;

    public override string ToString()
    {
        return $"\\{Name}{Scale}";
    }
}
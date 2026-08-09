namespace SekaiToolsBase.SubStationAlpha.Tag;

public class FontSpacing(float value) : Tag, INestableTag
{
    public float Value = value;
    public override string Name => "fsp";

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}
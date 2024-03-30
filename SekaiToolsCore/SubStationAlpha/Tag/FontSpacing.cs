namespace SekaiToolsCore.SubStationAlpha.Tag;

public class FontSpacing(float value) : Tag, INestableTag
{
    public override string Name => "fsp";
    public float Value = value;

    public override string ToString() => $"\\{Name}{Value}";
}
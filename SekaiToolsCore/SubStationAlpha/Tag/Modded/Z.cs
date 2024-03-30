namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class Z(int value) : Tag, INestableTag
{
    public override string Name => "z";

    public int Value { get; set; } = value;

    public override string ToString() => $"\\{Name}{Value}";
}
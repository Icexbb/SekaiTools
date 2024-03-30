namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Italic(bool value) : Tag
{
    public override string Name => "i";
    public bool Value = value;
    public override string ToString() => $"\\{Name}{(Value ? 1 : 0)}";
}
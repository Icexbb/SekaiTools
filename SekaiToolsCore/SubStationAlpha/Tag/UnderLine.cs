namespace SekaiToolsCore.SubStationAlpha.Tag;

public class UnderLine(bool value) : Tag
{
    public override string Name => "u";
    public bool Value = value;
    public override string ToString() => $"\\{Name}{(Value ? 1 : 0)}";
}
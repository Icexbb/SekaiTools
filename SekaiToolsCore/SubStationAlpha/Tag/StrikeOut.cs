namespace SekaiToolsCore.SubStationAlpha.Tag;

public class StrikeOut(bool value) : Tag
{
    public override string Name => "s";
    public bool Value = value;
    public override string ToString() => $"\\{Name}{(Value ? 1 : 0)}";
}
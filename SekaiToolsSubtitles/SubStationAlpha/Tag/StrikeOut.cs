namespace SekaiToolsBase.SubStationAlpha.Tag;

public class StrikeOut(bool value) : Tag
{
    public bool Value = value;
    public override string Name => "s";

    public override string ToString()
    {
        return $"\\{Name}{(Value ? 1 : 0)}";
    }
}
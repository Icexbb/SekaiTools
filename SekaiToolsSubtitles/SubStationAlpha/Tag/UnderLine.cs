namespace SekaiToolsBase.SubStationAlpha.Tag;

public class UnderLine(bool value) : Tag
{
    public bool Value = value;
    public override string Name => "u";

    public override string ToString()
    {
        return $"\\{Name}{(Value ? 1 : 0)}";
    }
}
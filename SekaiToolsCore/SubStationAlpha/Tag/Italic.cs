namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Italic(bool value) : Tag
{
    public bool Value = value;
    public override string Name => "i";

    public override string ToString()
    {
        return $"\\{Name}{(Value ? 1 : 0)}";
    }
}
namespace SekaiToolsBase.SubStationAlpha.Tag;

public class Reset(string? style) : Tag
{
    public override string Name => "r";

    public string Style { get; } = style ?? string.Empty;

    public override string ToString()
    {
        return $"\\{Name}{Style}";
    }
}
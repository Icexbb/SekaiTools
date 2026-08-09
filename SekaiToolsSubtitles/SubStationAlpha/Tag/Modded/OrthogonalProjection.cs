namespace SekaiToolsBase.SubStationAlpha.Tag.Modded;

public class OrthogonalProjection(bool value) : Tag
{
    public override string Name => "ortho";
    public bool Value { get; } = value;

    public override string ToString()
    {
        return $"\\{Name}{(Value ? "1" : "0")}";
    }
}
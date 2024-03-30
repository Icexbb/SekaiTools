namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public class OrthogonalProjection(bool value) : Tag
{
    public override string Name => "ortho";
    public bool Value { get; } = value;

    public override string ToString() => $"\\{Name}{(Value ? "1" : "0")}";
}
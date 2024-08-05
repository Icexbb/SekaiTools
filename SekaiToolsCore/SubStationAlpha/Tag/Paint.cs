namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Paint(int value) : Tag
{
    public override string Name => "p";

    public int Value { get; } = value;

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}

public class PaintBaselineOffset(int value) : Tag
{
    public override string Name => "pbo";

    public int Value { get; } = value;

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}
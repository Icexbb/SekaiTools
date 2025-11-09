namespace SekaiToolsBase.SubStationAlpha.Tag;

public enum LineBreakStyle
{
    Smart = 0,
    EndOfLine = 1,
    None = 2,
    SmartTrapezoid = 3
}

public class LineBreak(LineBreakStyle style) : Tag
{
    public LineBreakStyle Style { get; } = style;

    public override string Name => "q";

    public override string ToString()
    {
        return $"\\{Name}{(int)Style}";
    }
}
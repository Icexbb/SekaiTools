namespace SekaiToolsBase.SubStationAlpha.Tag;

public class FontSize : Tag, INestableTag
{
    public int Value;

    public FontSize(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public override string Name => "fs";

    public override string ToString()
    {
        return $"\\{Name}{Value}";
    }
}
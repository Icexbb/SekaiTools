namespace SekaiToolsCore.SubStationAlpha.Tag;

public class FontSize : Tag, INestableTag
{
    public override string Name => "fs";
    public int Value;

    public FontSize(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to 0.");
        Value = value;
    }

    public override string ToString() => $"\\{Name}{Value}";
}
namespace SekaiToolsCore.SubStationAlpha.Tag;

public class FontName : Tag
{
    public override string Name => "fn";
    public string Value;

    public FontName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentNullException(nameof(value), "Value cannot be null or whitespace.");
        Value = value.Trim();
    }

    public override string ToString() => $"\\{Name}{Value}";
}
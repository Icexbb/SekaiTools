namespace SekaiToolsCore.SubStationAlpha;

public class Styles(params Style[] items)
{
    private readonly Style[] _items = items;

    public Styles(string source) : this()
    {
        var sourceParts = source.Split('\n');
        _items = (from s in sourceParts where s.StartsWith("Style:") select new Style(s)).ToArray();
    }

    public override string ToString()
    {
        var result = _items.Aggregate(
            "[V4+ Styles]\n" +
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, " +
            "Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, " +
            "BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n",
            (current, item) => $"{current}{item}\n");
        return result.Trim();
    }
}
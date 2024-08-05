namespace SekaiToolsCore.SubStationAlpha;

public class Style
{
    private readonly int _angle, _borderStyle, _alignment;
    private readonly int _bold;
    private readonly int _encoding;

    private readonly string _fontName;
    private readonly int _italic;
    private readonly AlphaColor _primaryColour, _secondaryColour, _outlineColour, _backColour;
    private readonly int _scaleX;
    private readonly int _scaleY;
    private readonly double _spacing, _outline, _shadow;
    private readonly int _strikeOut;
    private readonly int _underline;
    public readonly int Fontsize;

    public Style(
        string name = "Default",
        string fontName = "Arial",
        int fontsize = 48,
        AlphaColor? primaryColour = null,
        AlphaColor? secondaryColour = null,
        AlphaColor? outlineColour = null,
        AlphaColor? backColour = null,
        int bold = 0, int italic = 0, int underline = 0, int strikeOut = 0,
        int scaleX = 100, int scaleY = 100, double spacing = 0.0,
        int angle = 0, int borderStyle = 1, double outline = 2.0, double shadow = 2.0,
        int alignment = 2, int marginL = 10, int marginR = 2, int marginV = 2, int encoding = 1
    )
    {
        Name = name;
        _fontName = fontName;
        Fontsize = fontsize;
        _primaryColour = primaryColour ?? AlphaColor.White;
        _secondaryColour = secondaryColour ?? AlphaColor.Red;
        _outlineColour = outlineColour ?? AlphaColor.Black;
        _backColour = backColour ?? AlphaColor.Black;
        _bold = bold;
        _italic = italic;
        _underline = underline;
        _strikeOut = strikeOut;
        _scaleX = scaleX;
        _scaleY = scaleY;
        _spacing = spacing;
        _angle = angle;
        _borderStyle = borderStyle;
        _outline = outline;
        _shadow = shadow;
        _alignment = alignment;
        MarginL = marginL;
        MarginR = marginR;
        MarginV = marginV;
        _encoding = encoding;
    }

    public Style(string source)
    {
        if (!source.StartsWith("Style: ")) throw new Exception("Source Not Start With Marker");
        var sourcePart = source[^6..].Split(',');
        if (sourcePart.Length != 23) throw new Exception("Source Parameter not Enough");
        Name = sourcePart[0];
        _fontName = sourcePart[1];
        if (int.TryParse(sourcePart[2], out Fontsize)) Fontsize = 50;
        _primaryColour = AlphaColor.FromString(sourcePart[3]);
        _secondaryColour = AlphaColor.FromString(sourcePart[4]);
        _outlineColour = AlphaColor.FromString(sourcePart[5]);
        _backColour = AlphaColor.FromString(sourcePart[6]);
        if (!int.TryParse(sourcePart[7], out _bold)) _bold = 0;
        if (!int.TryParse(sourcePart[8], out _italic)) _italic = 0;
        if (!int.TryParse(sourcePart[9], out _underline)) _underline = 0;
        if (!int.TryParse(sourcePart[10], out _strikeOut)) _strikeOut = 0;
        if (!int.TryParse(sourcePart[11], out _scaleX)) _scaleX = 100;
        if (!int.TryParse(sourcePart[12], out _scaleY)) _scaleY = 100;
        if (!double.TryParse(sourcePart[13], out _spacing)) _spacing = 0;
        if (!int.TryParse(sourcePart[14], out _angle)) _angle = 0;
        if (!int.TryParse(sourcePart[15], out _borderStyle)) _borderStyle = 1;
        if (!double.TryParse(sourcePart[16], out _outline)) _outline = 0;
        if (!double.TryParse(sourcePart[17], out _shadow)) _shadow = 0;
        if (!int.TryParse(sourcePart[18], out _alignment)) _alignment = 2;
        if (!int.TryParse(sourcePart[19], out var ml)) ml = 0;
        MarginL = ml;
        if (!int.TryParse(sourcePart[20], out var mr)) mr = 0;
        MarginR = mr;
        if (!int.TryParse(sourcePart[21], out var mv)) mv = 0;
        MarginV = mv;
        if (!int.TryParse(sourcePart[22], out _encoding)) _encoding = 1;
    }

    public int MarginL { get; }
    public int MarginR { get; }
    public int MarginV { get; }
    public string Name { get; }

    public override string ToString()
    {
        return
            $"Style: {Name},{_fontName},{Fontsize},{_primaryColour},{_secondaryColour},{_outlineColour}," +
            $"{_backColour},{_bold},{_italic},{_underline},{_strikeOut},{_scaleX},{_scaleY}," +
            $"{_spacing:0.0},{_angle},{_borderStyle},{_outline:0.0},{_shadow:0.0},{_alignment}," +
            $"{MarginL},{MarginR},{MarginV},{_encoding}";
    }
}
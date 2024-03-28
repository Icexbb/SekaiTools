using System.Globalization;
using System.Text;

namespace SekaiToolsCore;

public class AssDrawPoint(int x, int y)
{
    public int X = x, Y = y;

    public void Move(int x, int y)
    {
        X += x;
        Y += y;
    }

    public void Scale(float ratio)
    {
        X = (int)(X * ratio);
        Y = (int)(Y * ratio);
    }

    public void Scale(double ratio)
    {
        X = (int)(X * ratio);
        Y = (int)(Y * ratio);
    }
}

public abstract class AssDrawPart(string type)
{
    protected readonly string Type = type;

    public abstract void Move(int x, int y);
    public abstract void Scale(double ratio);
    public abstract void Scale(float ratio);
    public abstract override string ToString();
}

public class AssDrawMove(AssDrawPoint assDrawPoint) : AssDrawPart("m")
{
    public override void Move(int x, int y)
    {
        assDrawPoint.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        assDrawPoint.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        assDrawPoint.Scale(ratio);
    }

    public override string ToString()
    {
        return $"{Type} {assDrawPoint.X} {assDrawPoint.Y}";
    }
}

public class AssDrawLine(AssDrawPoint assDrawPoint) : AssDrawPart("l")
{
    public override void Move(int x, int y)
    {
        assDrawPoint.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        assDrawPoint.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        assDrawPoint.Scale(ratio);
    }

    public override string ToString()
    {
        return $"{Type} {assDrawPoint.X} {assDrawPoint.Y}";
    }
}

public class AssDrawBezier(AssDrawPoint assDrawPointA, AssDrawPoint assDrawPointB, AssDrawPoint assDrawPointC)
    : AssDrawPart("b")
{
    public override void Move(int x, int y)
    {
        assDrawPointA.Move(x, y);
        assDrawPointB.Move(x, y);
        assDrawPointC.Move(x, y);
    }

    public override void Scale(double ratio)
    {
        assDrawPointA.Scale(ratio);
        assDrawPointB.Scale(ratio);
        assDrawPointC.Scale(ratio);
    }

    public override void Scale(float ratio)
    {
        assDrawPointA.Scale(ratio);
        assDrawPointB.Scale(ratio);
        assDrawPointC.Scale(ratio);
    }

    public override string ToString()
    {
        return
            $"{Type} {assDrawPointA.X} {assDrawPointA.Y} {assDrawPointB.X} {assDrawPointB.Y} {assDrawPointC.X} {assDrawPointC.Y}";
    }
}

public class AssDraw
{
    private readonly AssDrawPart[] _parts;

    public AssDraw(AssDrawPart[] parts)
    {
        _parts = parts;
    }

    public AssDraw(string source)
    {
        source = source.ToLower();
        var sourceParts = source.Split(' ');
        var partsCount = sourceParts.Count(c => c is "m" or "l" or "b");
        var partList = new List<AssDrawPart>();
        for (var i = 0; i < sourceParts.Length; i++)
            switch (source[i])
            {
                case 'm':
                {
                    var x = int.Parse(sourceParts[i + 1]);
                    var y = int.Parse(sourceParts[i + 2]);
                    AssDrawMove move = new(new AssDrawPoint(x, y));
                    partList.Add(move);
                }
                    i += 2;
                    break;
                case 'l':
                {
                    var x = int.Parse(sourceParts[i + 1]);
                    var y = int.Parse(sourceParts[i + 2]);
                    AssDrawLine line = new(new AssDrawPoint(x, y));
                    partList.Add(line);
                }
                    i += 2;
                    break;
                case 'b':
                {
                    var x1 = int.Parse(sourceParts[i + 1]);
                    var y1 = int.Parse(sourceParts[i + 2]);
                    var x2 = int.Parse(sourceParts[i + 3]);
                    var y2 = int.Parse(sourceParts[i + 4]);
                    var x3 = int.Parse(sourceParts[i + 5]);
                    var y3 = int.Parse(sourceParts[i + 6]);
                    var bezier = new AssDrawBezier(new AssDrawPoint(x1, y1), new AssDrawPoint(x2, y2),
                        new AssDrawPoint(x3, y3));
                    partList.Add(bezier);
                }
                    i += 6;
                    break;
            }

        if (partsCount != partList.Count) throw new Exception("Draw Part Count Not Match");
        _parts = partList.ToArray();
    }

    public override string ToString()
    {
        var result = _parts.Aggregate("", (current, p) => $"{current} {p}");
        return result.Trim();
    }

    public void Move(int x, int y)
    {
        foreach (var t in _parts) t.Move(x, y);
    }

    public void Scale(float ratio)
    {
        foreach (var t in _parts) t.Scale(ratio);
    }

    public void Scale(double ratio)
    {
        foreach (var t in _parts) t.Scale(ratio);
    }
}

public class SubtitleScriptInfo(int playRexX, int playRexY, string title = "", string scriptType = "v4.00+")
{
    public override string ToString()
    {
        return
            $"[Script Info]\nTitle: {title}\nScriptType: {scriptType}\nPlayRexX: {playRexX}\nPlayRexY: {playRexY}";
    }
}

public class SubtitleGarbage(string video = "", string audio = "")
{
    public override string ToString()
    {
        return $"[Aegisub Project Garbage]\nAudio File: {audio}\nVideo File: {video}";
    }
}

public class SubtitleColor(int r, int g, int b, int a = 0)
{
    public int R { get; set; } = r;
    public int G { get; set; } = g;
    public int B { get; set; } = b;
    public int A { get; set; } = a;

    public override string ToString()
    {
        return $"&H{A:X2}{B:X2}{G:X2}{R:X2}&";
    }

    public static SubtitleColor FromString(string source)
    {
        if (!source.StartsWith("&H"))
            throw new Exception("Source Not Start With Marker");
        var sourcePart = source[2..].Replace("&", "");
        var r = 255;
        var g = 255;
        var b = 255;
        var a = 255;
        if (sourcePart.Length != 8)
        {
            b = int.Parse(sourcePart[0..2], NumberStyles.HexNumber);
            g = int.Parse(sourcePart[2..4], NumberStyles.HexNumber);
            r = int.Parse(sourcePart[4..6], NumberStyles.HexNumber);
        }
        else
        {
            a = int.Parse(sourcePart[0..2], NumberStyles.HexNumber);
            b = int.Parse(sourcePart[2..4], NumberStyles.HexNumber);
            g = int.Parse(sourcePart[4..6], NumberStyles.HexNumber);
            r = int.Parse(sourcePart[6..8], NumberStyles.HexNumber);
        }

        return new SubtitleColor(r, g, b, a);
    }
}

public class SubtitleStyleItem
{
    private readonly int _angle, _borderStyle, _alignment;
    public int MarginL { get; }
    public int MarginR { get; }
    public int MarginV { get; }
    private readonly int _encoding;
    private readonly int _fontsize, _bold, _italic, _underline, _strikeOut, _scaleX, _scaleY;
    public string Name { get; }

    private readonly string _fontName;
    private readonly SubtitleColor _primaryColour, _secondaryColour, _outlineColour, _backColour;
    private readonly double _spacing, _outline, _shadow;

    public SubtitleStyleItem(
        string name = "Default",
        string fontName = "Arial",
        int fontsize = 48,
        string primaryColour = "&HFFFFFF&", string secondaryColour = "&H0000FF&",
        string outlineColour = "&H000000&", string backColour = "&H000000&",
        int bold = 0, int italic = 0, int underline = 0, int strikeOut = 0,
        int scaleX = 100, int scaleY = 100, double spacing = 0.0,
        int angle = 0, int borderStyle = 1, double outline = 2.0, double shadow = 2.0,
        int alignment = 2, int marginL = 10, int marginR = 2, int marginV = 2, int encoding = 1
    )
    {
        Name = name;
        _fontName = fontName;
        _fontsize = fontsize;
        _primaryColour = SubtitleColor.FromString(primaryColour);
        _secondaryColour = SubtitleColor.FromString(secondaryColour);
        _outlineColour = SubtitleColor.FromString(outlineColour);
        _backColour = SubtitleColor.FromString(backColour);
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

    public SubtitleStyleItem(string source)
    {
        if (!source.StartsWith("Style:")) throw new Exception("Source Not Start With Marker");
        var sourcePart = source[^6..].Split(',');
        if (sourcePart.Length != 23) throw new Exception("Source Parameter not Enough");
        Name = sourcePart[0];
        _fontName = sourcePart[1];
        if (int.TryParse(sourcePart[2], out _fontsize)) _fontsize = 50;
        _primaryColour = SubtitleColor.FromString(sourcePart[3]);
        _secondaryColour = SubtitleColor.FromString(sourcePart[4]);
        _outlineColour = SubtitleColor.FromString(sourcePart[5]);
        _backColour = SubtitleColor.FromString(sourcePart[6]);
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

    public override string ToString()
    {
        return
            $"Style:{Name},{_fontName},{_fontsize},{_primaryColour},{_secondaryColour},{_outlineColour}," +
            $"{_backColour},{_bold},{_italic},{_underline},{_strikeOut},{_scaleX},{_scaleY}," +
            $"{_spacing:0.0},{_angle},{_borderStyle},{_outline:0.0},{_shadow:0.0},{_alignment}," +
            $"{MarginL},{MarginR},{MarginV},{_encoding}";
    }
}

public class SubtitleStyles
{
    private readonly SubtitleStyleItem[] _items;

    public SubtitleStyles(SubtitleStyleItem[] items)
    {
        _items = items;
    }

    public SubtitleStyles(string source)
    {
        var sourceParts = source.Split('\n');
        _items = (from s in sourceParts where s.StartsWith("Style:") select new SubtitleStyleItem(s)).ToArray();
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

public class SubtitleEventItem
{
    private readonly int _layer, _marginL, _marginR, _marginV;
    private readonly string _type, _style, _name, _effect;
    public string Start { get; set; }
    public string End { get; set; }
    public string Text { get; }

    private SubtitleEventItem(string type, int layer, string start, string end, string style, string name, int marginL,
        int marginR, int marginV, string effect, string text)
    {
        _type = type;
        _layer = layer;
        Start = start;
        End = end;
        _style = style;
        _name = name;
        _marginL = marginL;
        _marginR = marginR;
        _marginV = marginV;
        _effect = effect;
        Text = text;
    }

    public static SubtitleEventItem Dialog(string text,
        string start, string end, string style, int layer = 0, string name = "",
        int marginL = 0, int marginR = 0, int marginV = 0, string effect = "")
    {
        return new SubtitleEventItem(
            "Dialogue", layer, start, end, style, name, marginL, marginR, marginV, effect, text);
    }

    public static SubtitleEventItem Comment(string text,
        string start, string end, string style, int layer = 0, string name = "",
        int marginL = 0, int marginR = 0, int marginV = 0, string effect = "")
    {
        return new SubtitleEventItem(
            "Comment", layer, start, end, style, name, marginL, marginR, marginV, effect, text);
    }

    public SubtitleEventItem ToDialog()
    {
        return new SubtitleEventItem(
            "Dialogue", _layer, Start, End, _style, _name, _marginL, _marginR, _marginV, _effect, Text);
    }

    public SubtitleEventItem ToComment()
    {
        return new SubtitleEventItem(
            "Comment", _layer, Start, End, _style, _name, _marginL, _marginR, _marginV, _effect, Text);
    }

    public SubtitleEventItem(string source)
    {
        if (!source.StartsWith("Dialogue:") && !source.StartsWith("Comment:"))
            throw new Exception("Source Not Start With Marker");
        var typeSplit = source.Split(':', 2);
        var sourcePart = typeSplit[1].Split(',');
        if (sourcePart.Length != 10) throw new Exception("Source Parameter not Enough");
        _type = typeSplit[0].Replace(':', ' ').Trim();

        if (int.TryParse(sourcePart[0], out _layer)) _layer = 0;
        Start = sourcePart[1].Trim();
        End = sourcePart[2].Trim();
        _style = sourcePart[3].Trim();
        _name = sourcePart[4].Trim();
        if (int.TryParse(sourcePart[5], out _marginL)) _marginL = 0;
        if (int.TryParse(sourcePart[6], out _marginR)) _marginR = 0;
        if (int.TryParse(sourcePart[7], out _marginV)) _marginV = 0;
        _effect = sourcePart[8].Trim();
        Text = sourcePart[9].Trim();
    }

    public override string ToString()
    {
        return $"{_type}: {_layer},{Start},{End},{_style},{_name},{_marginL},{_marginR},{_marginV},{_effect},{Text}";
    }
}

public class SubtitleEvents
{
    private readonly List<SubtitleEventItem> _subtitleEventItems;

    public SubtitleEvents()
    {
        _subtitleEventItems = new List<SubtitleEventItem>();
    }

    public SubtitleEvents(IEnumerable<SubtitleEventItem> items)
    {
        _subtitleEventItems = items.ToList();
    }

    public SubtitleEvents(string source)
    {
        _subtitleEventItems = (
            from s in source.Split('\n')
            where s.StartsWith("Dialogue:") || s.StartsWith("Comment:")
            select new SubtitleEventItem(s)
        ).ToList();
    }

    public override string ToString()
    {
        List<string> sb =
            ["[Events]", "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"];
        sb.AddRange(_subtitleEventItems.Select(subtitleEventItem => subtitleEventItem.ToString()));
        return string.Join("\n", sb).Trim();
    }

    public void Add(SubtitleEventItem item) => _subtitleEventItems.Add(item);
}

public class Subtitle(
    SubtitleScriptInfo scriptInfo,
    SubtitleGarbage garbage,
    SubtitleStyles styles,
    SubtitleEvents events)
{
    public override string ToString()
    {
        return $"{scriptInfo}\n\n{garbage}\n\n{styles}\n\n{events}\n";
    }

    public void Save(string path)
    {
        File.OpenWrite(path).Write(Encoding.UTF8.GetBytes(ToString()));
    }
}
using System.Globalization;

namespace SekaiToolsCore.SubStationAlpha;

public class Color(int r, int g, int b)
{
    public int R { get; } = r;
    public int G { get; } = g;
    public int B { get; } = b;


    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Red => new(255, 0, 0);
    public static Color Green => new(0, 255, 0);
    public static Color Blue => new(0, 0, 255);
    public static Color Yellow => new(255, 255, 0);
    public static Color Cyan => new(0, 255, 255);
    public static Color Magenta => new(255, 0, 255);
    public static Color Gray => new(128, 128, 128);
    public static Color DarkGray => new(64, 64, 64);
    public static Color LightGray => new(192, 192, 192);

    public override string ToString()
    {
        return $"&H{B:X2}{G:X2}{R:X2}&";
    }

    public static Color FromString(string source)
    {
        if (!source.StartsWith("&H"))
            throw new Exception("Source Not Start With Marker");
        var sourcePart = source[2..].Replace("&", "").Replace("H", "");

        if (sourcePart.Length != 6) throw new Exception("Source Parameter not Enough");

        var b = int.Parse(sourcePart[..2], NumberStyles.HexNumber);
        var g = int.Parse(sourcePart[2..4], NumberStyles.HexNumber);
        var r = int.Parse(sourcePart[4..6], NumberStyles.HexNumber);
        return new Color(r, g, b);
    }

    public static AlphaColor operator +(Color color, Alpha alpha)
    {
        return new AlphaColor(alpha.A, color.R, color.G, color.B);
    }

    public static AlphaColor operator +(Alpha alpha, Color color)
    {
        return new AlphaColor(alpha.A, color.R, color.G, color.B);
    }
}
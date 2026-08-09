using System.Globalization;

namespace SekaiToolsBase.SubStationAlpha;

public class AlphaColor(int a, int r, int g, int b)
{
    public int A { get; } = a;
    public int R { get; } = r;
    public int G { get; } = g;
    public int B { get; } = b;

    public static AlphaColor White => new(0, 255, 255, 255);
    public static AlphaColor Black => new(0, 0, 0, 0);
    public static AlphaColor Red => new(0, 255, 0, 0);
    public static AlphaColor Green => new(0, 0, 255, 0);
    public static AlphaColor Blue => new(0, 0, 0, 255);
    public static AlphaColor Yellow => new(0, 255, 255, 0);
    public static AlphaColor Cyan => new(0, 0, 255, 255);
    public static AlphaColor Magenta => new(0, 255, 0, 255);
    public static AlphaColor Gray => new(0, 128, 128, 128);
    public static AlphaColor DarkGray => new(0, 64, 64, 64);
    public static AlphaColor LightGray => new(0, 192, 192, 192);

    public override string ToString()
    {
        return $"&H{A:X2}{B:X2}{G:X2}{R:X2}&";
    }

    public static AlphaColor FromString(string source)
    {
        if (!source.StartsWith("&H"))
            throw new Exception("Source Not Start With Marker");
        var sourcePart = source[2..].Replace("&", "").Replace("H", "");

        if (sourcePart.Length != 8) throw new Exception("Source Parameter not Enough");

        var a = int.Parse(sourcePart[..2], NumberStyles.HexNumber);
        var b = int.Parse(sourcePart[2..4], NumberStyles.HexNumber);
        var g = int.Parse(sourcePart[4..6], NumberStyles.HexNumber);
        var r = int.Parse(sourcePart[6..8], NumberStyles.HexNumber);

        return new AlphaColor(a, r, g, b);
    }

    public static explicit operator AlphaColor(Color color)
    {
        return new AlphaColor(0, color.R, color.G, color.B);
    }
}
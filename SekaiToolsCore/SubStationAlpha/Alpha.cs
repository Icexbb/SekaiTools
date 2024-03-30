namespace SekaiToolsCore.SubStationAlpha;

public class SubtitleAlpha(int a)
{
    public int A { get; } = a;

    public override string ToString() => $"&H{A:X2}&";

    public static SubtitleAlpha Transparent => new(0);

    public static SubtitleAlpha Opaque => new(255);
}
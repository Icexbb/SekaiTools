namespace SekaiToolsCore.SubStationAlpha;

public class Alpha(int a)
{
    public int A { get; } = a;

    public override string ToString() => $"&H{A:X2}&";

    public static Alpha Transparent => new(0);

    public static Alpha Opaque => new(255);
}
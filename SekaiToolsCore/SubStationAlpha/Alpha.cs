namespace SekaiToolsCore.SubStationAlpha;

public class Alpha(int a)
{
    public int A { get; } = a;

    public static Alpha Transparent => new(0);

    public static Alpha Opaque => new(255);

    public override string ToString()
    {
        return $"&H{A:X2}&";
    }
}
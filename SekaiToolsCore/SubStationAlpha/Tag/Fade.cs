namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Fade(int start, int end) : Tag
{
    public override string Name => "fad";
    public int Start { get; } = start;
    public int End { get; } = end;

    public override string ToString() => $"\\{Name}({Start},{End})";
}

public class FadeComplex : Tag
{
    public int Alpha1 { get; }
    public int Alpha2 { get; }
    public int Alpha3 { get; }

    public int Time1 { get; }
    public int Time2 { get; }
    public int Time3 { get; }
    public int Time4 { get; }

    public FadeComplex(int alpha1, int alpha2, int alpha3, int time1, int time2, int time3, int time4)
    {
        if (alpha1 is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(alpha1), "Alpha1 must be between 0 and 255.");
        if (alpha2 is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(alpha2), "Alpha2 must be between 0 and 255.");
        if (alpha3 is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(alpha3), "Alpha3 must be between 0 and 255.");
        Alpha1 = alpha1;
        Alpha2 = alpha2;
        Alpha3 = alpha3;
        Time1 = time1;
        Time2 = time2;
        Time3 = time3;
        Time4 = time4;
    }


    public override string Name => "fade";

    public override string ToString() => $"\\{Name}({Alpha1},{Alpha2},{Alpha3},{Time1},{Time2},{Time3},{Time4})";
}
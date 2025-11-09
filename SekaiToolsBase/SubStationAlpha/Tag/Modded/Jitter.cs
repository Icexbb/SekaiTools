namespace SekaiToolsBase.SubStationAlpha.Tag.Modded;

public class Jitter(int left, int right, int up, int down, int period, int? seed = null)
    : Tag, INestableTag
{
    public override string Name => "jitter";

    public int Left { get; set; } = left;
    public int Right { get; set; } = right;
    public int Up { get; set; } = up;
    public int Down { get; set; } = down;

    public int Period { get; set; } = period;
    public int? Seed { get; set; } = seed;

    public override string ToString()
    {
        var seed = Seed.HasValue ? $",{Seed}" : string.Empty;
        return $"\\{Name}({Left},{Right},{Up},{Down},{Period}{seed})";
    }
}
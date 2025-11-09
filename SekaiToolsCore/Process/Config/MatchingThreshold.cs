namespace SekaiToolsCore.Process.Config;

public struct MatchingThreshold()
{
    public double DialogNametagNormal { get; init; } = 0.80;
    public double DialogNametagSpecial { get; init; } = 0.80;
    public double DialogContentNormal { get; init; } = 0.80;
    public double DialogContentSpecial { get; init; } = 0.80;
    public double BannerNormal { get; init; } = 0.75;
    public double MarkerNormal { get; init; } = 0.75;
}
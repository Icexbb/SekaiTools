namespace SekaiToolsMedia;

public sealed record VideoSuppressionOptions(
    string SourceVideo,
    string SourceSubtitle,
    string OutputPath,
    string X264Parameters,
    int SourceFrameCount = 0);

public sealed record VideoSuppressionProgress(
    int ProcessedFrames,
    int TotalFrames,
    double FramesPerSecond,
    bool Running,
    string Status)
{
    public double Fraction => TotalFrames > 0
        ? Math.Clamp((double)ProcessedFrames / TotalFrames, 0, 1)
        : 0;
}

namespace SekaiToolsCore.Match.TemplateMatcher;

public sealed record MatcherDiagnostic(
    string Matcher,
    int TargetIndex,
    int FrameIndex,
    string Reason);

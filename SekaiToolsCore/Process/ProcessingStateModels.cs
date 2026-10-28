namespace SekaiToolsCore.Process;

public record PointDto(int X, int Y);

public record FrameResultDto(int Index, int X, int Y);

public class MatcherDiagnosticDto
{
    public string Matcher { get; init; } = "";
    public int TargetIndex { get; init; }
    public int FrameIndex { get; init; }
    public string Reason { get; init; } = "";
}

public class DialogFrameSetDto
{
    public bool Finished { get; init; }
    public bool UseSeparator { get; init; }
    public int SeparateFrame { get; init; }
    public int SeparatorContentIndex { get; init; }
    public List<FrameResultDto> Frames { get; init; } = [];
}

public class BannerFrameSetDto
{
    public bool Finished { get; init; }
    public int Start { get; init; } = -1;
    public int End { get; init; } = -1;
}

public class MarkerFrameSetDto
{
    public bool Finished { get; init; }
    public List<FrameResultDto> Frames { get; init; } = [];
}

public sealed record AdaptiveSearchStateDto(int LastSampleFrameIndex, int PreviousSkippedFrameIndex);

public sealed record LookaheadStateDto(int FirstFailureFrame, int TargetIndex)
{
    public int FailedProbes { get; init; }
    public int LastProbeFrame { get; init; } = -1;
    public byte[]? Evidence { get; init; }
    public bool ChangePending { get; init; }
}

public class DialogMatcherStateDto
{
    public int PendingResumeStatus { get; init; } = 3;
    public List<FrameResultDto> PendingTrackingFrames { get; init; } = [];
    public AdaptiveSearchStateDto? SearchState { get; init; }
    public LookaheadStateDto? LookaheadState { get; init; }
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int? LastFailureFrame { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public PointDto? NameTagPosition { get; init; }
    public List<DialogFrameSetDto> FrameSets { get; init; } = [];
    public List<MatcherDiagnosticDto> Diagnostics { get; init; } = [];
}

public class BannerMatcherStateDto
{
    public int? PendingLossFrame { get; init; }
    public AdaptiveSearchStateDto? SearchState { get; init; }
    public LookaheadStateDto? LookaheadState { get; init; }
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int? LastFailureFrame { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public List<BannerFrameSetDto> FrameSets { get; init; } = [];
    public List<MatcherDiagnosticDto> Diagnostics { get; init; } = [];
}

public class MarkerMatcherStateDto
{
    public int? PendingLossFrame { get; init; }
    public AdaptiveSearchStateDto? SearchState { get; init; }
    public LookaheadStateDto? LookaheadState { get; init; }
    public int Status { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int? LastFailureFrame { get; init; }
    public int LastFailedIndex { get; init; } = -1;
    public bool UseFallbackThreshold { get; init; }
    public List<MarkerFrameSetDto> FrameSets { get; init; } = [];
    public List<MatcherDiagnosticDto> Diagnostics { get; init; } = [];
}

public sealed record TemplateScaleStateDto(int Usage, double? Scale, int LowConfidenceCount);

public class ProcessingState
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ReusesCompletedFrames { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<int[]>? CheckpointTimecodeBlocks { get; init; }
    public string Version { get; set; } = ProcessingStateCompatibility.CurrentVersion;
    public ProcessingStateMetadata? Metadata { get; set; }
    public ProcessStopReason StopReason { get; init; }
    public int FrameIndex { get; init; }
    public bool ContentFinished { get; init; }
    public string VideoFilePath { get; init; } = "";
    public string ScriptFilePath { get; init; } = "";
    public string TranslateFilePath { get; init; } = "";
    public List<int> Timecodes { get; init; } = [];
    public DialogMatcherStateDto? Dialog { get; init; }
    public BannerMatcherStateDto? Banner { get; init; }
    public MarkerMatcherStateDto? Marker { get; init; }
    public List<TemplateScaleStateDto>? TemplateScales { get; init; }
}

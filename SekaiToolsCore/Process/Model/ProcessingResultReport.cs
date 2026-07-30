using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsCore.Process.Model;

public enum ProcessingOutcome
{
    NotStarted,
    Complete,
    Partial,
    Failed,
    Canceled
}

public sealed record UnmatchedEventInfo(string Type, int Index, string Content, string Reason);

public sealed record ProcessingResultReport(
    ProcessingOutcome Outcome,
    ProcessStopReason StopReason,
    int RecognizedDialogs,
    int TotalDialogs,
    int RecognizedBanners,
    int TotalBanners,
    int RecognizedMarkers,
    int TotalMarkers,
    IReadOnlyList<UnmatchedEventInfo> UnmatchedEvents)
{
    public int RecognizedTotal => RecognizedDialogs + RecognizedBanners + RecognizedMarkers;
    public int Total => TotalDialogs + TotalBanners + TotalMarkers;
    public bool CanExport => RecognizedTotal > 0;

    public string Summary =>
        $"结果={OutcomeText(Outcome)}；对话={RecognizedDialogs}/{TotalDialogs}；横幅={RecognizedBanners}/{TotalBanners}；" +
        $"标记={RecognizedMarkers}/{TotalMarkers}；未识别={UnmatchedEvents.Count}";

    public static ProcessingResultReport Create(
        ProcessStopReason stopReason,
        IReadOnlyList<DialogBaseFrameSet> dialogs,
        IReadOnlyList<BannerBaseFrameSet> banners,
        IReadOnlyList<MarkerBaseFrameSet> markers,
        IReadOnlyList<MatcherDiagnostic> diagnostics)
    {
        var unmatched = new List<UnmatchedEventInfo>();
        AddUnmatched(dialogs, "对话", diagnostics, unmatched,
            item => $"{item.Data.CharacterOriginal}：{item.Data.BodyOriginal}");
        AddUnmatched(banners, "横幅", diagnostics, unmatched, item => item.Data.BodyOriginal);
        AddUnmatched(markers, "标记", diagnostics, unmatched, item => item.Data.BodyOriginal);

        var recognizedDialogs = dialogs.Count(item => !item.IsEmpty());
        var recognizedBanners = banners.Count(item => !item.IsEmpty());
        var recognizedMarkers = markers.Count(item => !item.IsEmpty());
        var recognizedTotal = recognizedDialogs + recognizedBanners + recognizedMarkers;
        var outcome = ResolveOutcome(stopReason, recognizedTotal, unmatched.Count);

        return new ProcessingResultReport(
            outcome,
            stopReason,
            recognizedDialogs,
            dialogs.Count,
            recognizedBanners,
            banners.Count,
            recognizedMarkers,
            markers.Count,
            unmatched);
    }

    internal static ProcessingOutcome ResolveOutcome(
        ProcessStopReason stopReason,
        int recognizedTotal,
        int unmatchedCount)
    {
        if (stopReason == ProcessStopReason.None)
            return ProcessingOutcome.NotStarted;
        if (stopReason == ProcessStopReason.Canceled)
            return ProcessingOutcome.Canceled;
        if (stopReason == ProcessStopReason.Completed && unmatchedCount == 0)
            return ProcessingOutcome.Complete;
        return recognizedTotal > 0 ? ProcessingOutcome.Partial : ProcessingOutcome.Failed;
    }

    private static void AddUnmatched<T>(
        IReadOnlyList<T> frameSets,
        string type,
        IReadOnlyList<MatcherDiagnostic> diagnostics,
        ICollection<UnmatchedEventInfo> result,
        Func<T, string> getContent) where T : BaseFrameSet
    {
        for (var index = 0; index < frameSets.Count; index++)
        {
            var frameSet = frameSets[index];
            if (!frameSet.IsEmpty()) continue;

            var reason = diagnostics.LastOrDefault(item =>
                item.Matcher == typeof(T).Name && item.TargetIndex == index)?.Reason ?? "处理结束前未识别";
            result.Add(new UnmatchedEventInfo(type, index, Normalize(getContent(frameSet)), reason));
        }
    }

    private static string Normalize(string value)
    {
        return value.Replace("\\N", " ").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string OutcomeText(ProcessingOutcome outcome)
    {
        return outcome switch
        {
            ProcessingOutcome.NotStarted => "未开始",
            ProcessingOutcome.Complete => "完整",
            ProcessingOutcome.Partial => "部分完成",
            ProcessingOutcome.Failed => "失败",
            ProcessingOutcome.Canceled => "已取消",
            _ => outcome.ToString()
        };
    }
}

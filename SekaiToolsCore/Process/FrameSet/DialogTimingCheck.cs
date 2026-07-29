using SekaiToolsBase.Utils;

namespace SekaiToolsCore.Process.FrameSet;

public record DialogTimingIssue(string LineName, int CharacterCount, int RequiredMilliseconds,
    int AvailableMilliseconds)
{
    public string Warning =>
        $"{LineName}文字将无法显示完全（{CharacterCount}字至少需要{RequiredMilliseconds}ms，实际{AvailableMilliseconds}ms）";
}

public static class DialogTimingCheck
{
    public static IReadOnlyList<DialogTimingIssue> GetIssues(DialogBaseFrameSet set, int charTime)
    {
        if (charTime <= 0 || set.Frames.Count == 0) return [];

        var content = set.Data.FinalContent.TrimAll();
        if (!set.UseSeparator) return CheckLine("本行", content, set.Frames.Count, set.Fps.Fps(), charTime);

        var separatorIndex = set.Separate.SeparatorContentIndex;
        if (separatorIndex <= 0 || separatorIndex >= content.Length) separatorIndex = content.Length / 2;

        var firstFrameCount = set.Separate.SeparateFrame - set.StartIndex();
        if (firstFrameCount <= 0 || firstFrameCount >= set.Frames.Count) firstFrameCount = set.Frames.Count / 2;

        var issues = new List<DialogTimingIssue>();
        issues.AddRange(CheckLine("第一行", content[..separatorIndex], firstFrameCount, set.Fps.Fps(), charTime));
        issues.AddRange(CheckLine("第二行", content[separatorIndex..], set.Frames.Count - firstFrameCount,
            set.Fps.Fps(), charTime));
        return issues;
    }

    private static IReadOnlyList<DialogTimingIssue> CheckLine(string lineName, string content, int frameCount,
        double fps, int charTime)
    {
        var requiredMilliseconds = content.Length * charTime;
        var availableMilliseconds = frameCount * 1000 / fps;
        return requiredMilliseconds > availableMilliseconds
            ? [new DialogTimingIssue(lineName, content.Length, requiredMilliseconds,
                (int)Math.Round(availableMilliseconds))]
            : [];
    }
}

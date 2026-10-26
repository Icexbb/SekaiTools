using System.Drawing;
using SekaiToolsBase;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class DialogTemplateMatcher(
    VideoInfo videoInfo,
    Story storyData,
    TemplateManager templateManager,
    TemplateMatchCachePool cachePool,
    Config config
) : MatcherStateMachine<DialogBaseFrameSet>(
    storyData.Dialogs().Select(d => new DialogBaseFrameSet(d, videoInfo.Fps)).ToList(),
    (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5)
), IDisposable
{
    private const int MaxLookaheadTargets = 3;
    private readonly FiniteLookaheadTracker _lookaheadTracker = new();
    private readonly int _lookaheadTriggerFrames = (int)Math.Ceiling(videoInfo.Fps.Fps() * 3);
    private readonly AdaptiveSearchScheduler _searchScheduler = new();
    private Point _nameTagPosition;
    private MatchStatus _status;
    private readonly DialogTrackingGrace _trackingGrace = new(videoInfo.Fps.Fps());
    private readonly Dictionary<string, GaMat> _nameTemplates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (GaMat First, GaMat Second, GaMat Third)> _contentTemplates = new(StringComparer.Ordinal);
    private readonly TemplateMatchCachePool _lookaheadEvidencePool = new();

    public void Dispose()
    {
        _searchScheduler.Dispose();
        _nameTemplates.Clear();
        _contentTemplates.Clear();
        _lookaheadEvidencePool.ResetAll();
    }

    public int LastNotProcessedIndex()
    {
        return NextUnfinishedIndex();
    }

    private GaMat GetNameTag(string name)
    {
        return templateManager.GetMatchTemplate(TemplateUsage.DialogNameTag, name);
    }

    private Point DialogMatchNameTag(FrameMatchContext frame, DialogBaseFrameSet dialogBase, int frameIndex = -1)
    {
        var content = dialogBase.Data.CharacterOriginal;
        if (string.IsNullOrWhiteSpace(content)) return Point.Empty;
        var contentLen = content.Length;
        if (!_nameTemplates.TryGetValue(content, out var template))
            _nameTemplates[content] = template = GetNameTag(TrimTemplateContent(content));
        var res = LocalMatch(frame, template,
            dialogBase.Data.Shake
                ? config.MatchingThreshold.DialogNametagSpecial
                : config.MatchingThreshold.DialogNametagNormal);
        if (!res.IsEmpty && _nameTagPosition.IsEmpty) _nameTagPosition = res;
        return res;

        Point LocalMatch(FrameMatchContext src, GaMat tmp, double threshold)
        {
            var roi = LocalGetCropArea();
            if (roi.Width < tmp.Size.Width || roi.Height < tmp.Size.Height)
                return Point.Empty;
            var result = TemplateMatcher.Match(src, roi, tmp, cachePool,
                TemplateMatchCachePool.MatchUsage.DialogNameTag);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Name Tag {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);

            if (result.IsMatch(EffectiveThreshold(threshold, IsStatusMatched(_status))))
                return new Point(result.MaxLoc.X + roi.X, result.MaxLoc.Y + roi.Y);

            return Point.Empty;
        }

        Rectangle LocalGetCropArea()
        {
            var offset = TemplateManager.GetFontSize(frame.Size);
            var dialogAreaSize = GetDialogAreaSize();
            var rect = new Rectangle
            {
                X = (videoInfo.Resolution.Width - dialogAreaSize.Width - (int)(offset * 2f)) / 2,
                Y = (videoInfo.Resolution.Height - dialogAreaSize.Height - (int)(offset * 2f)) / 1,
                Height = (int)(offset * 3f),
                Width = (int)(offset * contentLen * 3f)
            };
            if (dialogBase.IsJitter) rect.Extend(1.5);

            rect.Limit(new Rectangle(Point.Empty, videoInfo.Resolution));
            return rect;
        }

        Size GetDialogAreaSize()
        {
            return videoInfo.FrameRatio > 16.0 / 9
                ? new Size
                {
                    Height = (int)(0.237 * videoInfo.Resolution.Height),
                    Width = (int)(1.389 * videoInfo.Resolution.Height)
                }
                : new Size
                {
                    Height = (int)(0.133 * videoInfo.Resolution.Width),
                    Width = (int)(0.781 * videoInfo.Resolution.Width)
                };
        }

    }

    internal static string TrimTemplateContent(string origin, int maxLen = 3)
    {
        var trimmed = "";
        var len = 0D;
        foreach (var c in origin)
        {
            trimmed += c;
            len += char.IsAscii(c) ? 0.5 : 1;
            if (len >= maxLen) break;
        }
        if (trimmed.Contains('・')) trimmed = trimmed[..trimmed.IndexOf('・')];
        return trimmed;
    }

    private MatchStatus DialogMatchContent(FrameMatchContext frame, DialogBaseFrameSet dialogBase, Point point,
        MatchStatus lastStatus = 0,
        int frameIndex = -1)
    {
        var content = dialogBase.Data.BodyOriginal;
        if (point.IsEmpty || string.IsNullOrEmpty(content)) return MatchStatus.DialogNotMatched;
        var (template1, template2, template3) = GetContentTemplates(content);

        bool matchRes;
        var matchingThreshold = dialogBase.Data.Shake
            ? config.MatchingThreshold.DialogContentSpecial
            : config.MatchingThreshold.DialogContentNormal;

        switch (lastStatus)
        {
            case MatchStatus.DialogNotMatched:
                {
                    matchRes = LocalMatch(frame, template1, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent1);
                    return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogNotMatched;
                }
            case MatchStatus.DialogMatched1:
                {
                    matchRes = LocalMatch(frame, template2, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent2);
                    if (matchRes) return MatchStatus.DialogMatched2;
                    matchRes = LocalMatch(frame, template1, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent1);
                    return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogDropped;
                }
            case MatchStatus.DialogMatched2:
                {
                    matchRes = LocalMatch(frame, template3, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent3);
                    if (matchRes) return MatchStatus.DialogMatched3;
                    matchRes = LocalMatch(frame, template2, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent2);
                    return matchRes ? MatchStatus.DialogMatched2 : MatchStatus.DialogDropped;
                }
            case MatchStatus.DialogMatched3:
            case MatchStatus.DialogPending:
                {
                    matchRes = LocalMatch(frame, template3, matchingThreshold,
                        TemplateMatchCachePool.MatchUsage.DialogContent3);
                    if (matchRes) return MatchStatus.DialogMatched3;
                    // Only a fresh name match plus a two-glyph match may bridge a
                    // short loss of the three-glyph template. Never lower thresholds.
                    if (!string.IsNullOrWhiteSpace(dialogBase.Data.CharacterOriginal) &&
                        !DialogMatchNameTag(frame, dialogBase, frameIndex).IsEmpty &&
                        LocalMatch(frame, template2, matchingThreshold,
                            TemplateMatchCachePool.MatchUsage.DialogContent2))
                        return MatchStatus.DialogPending;
                    return MatchStatus.DialogDropped;
                }
            case MatchStatus.NameTagNotMatched:
            case MatchStatus.DialogDropped:
            default:
                return MatchStatus.DialogNotMatched;
        }

        bool LocalMatch(FrameMatchContext src, GaMat tmp, double threshold,
            TemplateMatchCachePool.MatchUsage usage)
        {
            var offset = TemplateManager.GetFontSize(src.Size);
            var dialogStartPosition = new Rectangle
            {
                X = point.X + (int)(-0.5f * offset),
                Y = point.Y + (int)(1f * offset),
                Width = (int)(5.0f * offset),
                Height = (int)(2.0f * offset)
            };
            if (dialogBase.IsJitter) dialogStartPosition.Extend(1.5);

            dialogStartPosition.Limit(new Rectangle(Point.Empty, videoInfo.Resolution));
            if (dialogStartPosition.Width < tmp.Size.Width || dialogStartPosition.Height < tmp.Size.Height)
                return false;

            var effectiveThreshold = EffectiveThreshold(threshold, IsStatusMatched(lastStatus));
            var result = TemplateMatcher.MatchContent(src, dialogStartPosition, tmp, cachePool, usage, effectiveThreshold,
                allowScaleRecovery: IsStatusMatched(lastStatus));

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Dialog Content {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);

            return result.IsMatch(effectiveThreshold);
        }

    }

    internal (GaMat First, GaMat Second, GaMat Third) GetContentTemplates(string content)
    {
        if (_contentTemplates.TryGetValue(content, out var cached)) return cached;
        var dialogBody1 = content[..Math.Min(1, content.Length)];
        var dialogBody2 = content[..Math.Min(2, content.Length)];
        var dialogBody3 = content[..Math.Min(3, content.Length)];
        var template1 = templateManager.GetMatchTemplate(TemplateUsage.DialogContent, dialogBody1);
        var template2 = templateManager.GetMatchTemplate(TemplateUsage.DialogContent, dialogBody2);
        var template3 = templateManager.GetMatchTemplate(TemplateUsage.DialogContent, dialogBody3);
        var result = (template1, template2, template3);
        _contentTemplates.Add(content, result);
        return result;
    }

    // Lookahead must identify a different line, rather than another instance of
    // the same speaker's opening parenthesis or shared punctuation prefix.
    internal static string LookaheadContentPrefix(DialogStoryEvent candidate, IEnumerable<DialogStoryEvent> earlier)
    {
        var content = FirstLine(candidate.BodyOriginal);
        if (content.Length == 0) return string.Empty;
        var length = Math.Min(3, content.Length);
        var name = TrimTemplateContent(candidate.CharacterOriginal);
        foreach (var previous in earlier)
        {
            if (TrimTemplateContent(previous.CharacterOriginal) != name) continue;
            var other = FirstLine(previous.BodyOriginal);
            var shared = 0;
            while (shared < content.Length && shared < other.Length && content[shared] == other[shared]) shared++;
            // Identical first lines and very long shared prefixes cannot be
            // distinguished by this bounded verification window.
            if (shared == content.Length || shared >= 12) return string.Empty;
            length = Math.Max(length, Math.Min(12, Math.Min(content.Length, shared + 3)));
        }
        return content[..length];

        static string FirstLine(string body)
        {
            var end = body.IndexOfAny(['\r', '\n']);
            return end < 0 ? body : body[..end];
        }
    }

    private bool ConfirmLookaheadTarget(FrameMatchContext frame, int currentIndex, int candidateIndex,
        Point point, int frameIndex)
    {
        var candidate = Set[candidateIndex];
        var prefix = LookaheadContentPrefix(candidate.Data,
            Set.Skip(Math.Max(0, currentIndex - 1)).Take(candidateIndex - Math.Max(0, currentIndex - 1))
                .Select(dialog => dialog.Data));
        if (prefix.Length == 0 || point.IsEmpty) return false;
        var template = templateManager.GetMatchTemplate(TemplateUsage.DialogContent, prefix);
        var font = TemplateManager.GetFontSize(frame.Size);
        var region = new Rectangle(point.X - (int)(0.5f * font), point.Y + font,
            Math.Max(font * 5, template.Size.Width + font * 2), font * 2);
        if (candidate.IsJitter) region.Extend(1.5);
        region.Limit(new Rectangle(Point.Empty, frame.Size));
        if (region.Width < template.Size.Width || region.Height < template.Size.Height) return false;
        // Verification is transient and searches all supported scales. It must
        // not calibrate the main matcher or become part of checkpoint state.
        _lookaheadEvidencePool.ResetAll();
        _lookaheadEvidencePool.SetFrameIndex(frameIndex);
        var result = TemplateMatcher.Match(frame, region, template, _lookaheadEvidencePool);
        var threshold = Math.Max(0.85, candidate.Data.Shake
            ? config.MatchingThreshold.DialogContentSpecial : config.MatchingThreshold.DialogContentNormal);
        return result.IsMatch(threshold);
    }

    // Frame sets retain a name-tag-equivalent anchor so tracking, checkpoints and
    // subtitle layout use the same coordinate convention for both dialogue kinds.
    internal static Point ContentToDialogAnchor(Point contentPosition, int fontSize) =>
        new(contentPosition.X + (int)(0.5f * fontSize), contentPosition.Y - fontSize);

    private Point DialogMatchUnnamed(FrameMatchContext frame, DialogBaseFrameSet dialog)
    {
        var content = dialog.Data.BodyOriginal;
        if (string.IsNullOrWhiteSpace(content)) return Point.Empty;
        var font = TemplateManager.GetFontSize(frame.Size);
        var height = videoInfo.FrameRatio > 16.0 / 9 ? frame.Size.Height * 0.237 : frame.Size.Width * 0.133;
        var width = videoInfo.FrameRatio > 16.0 / 9 ? frame.Size.Height * 1.389 : frame.Size.Width * 0.781;
        var region = new Rectangle((int)((frame.Size.Width - width) / 2 - font),
            (int)(frame.Size.Height - height - font), font * 8, font * 4);
        // Once acquired, keep searching near the actual body rather than elsewhere
        // in the scene. Shake still gets a bounded window around its last position.
        if (!dialog.IsEmpty())
        {
            var anchor = dialog.End().Point;
            region = new Rectangle(anchor.X - font, anchor.Y, font * 6, font * 4);
        }
        region.Limit(new Rectangle(Point.Empty, frame.Size));
        var templates = GetContentTemplates(content);
        var candidates = new[] { templates.Third, templates.Second, templates.First };
        foreach (var template in candidates.Distinct())
        {
            if (region.Width < template.Size.Width || region.Height < template.Size.Height) continue;
            // A single glyph has no name-tag evidence: never relax its acquisition
            // threshold through the generic fallback used for named dialogue.
            var threshold = Math.Max(template == templates.First ? 0.90 : 0.85,
                dialog.Data.Shake ? config.MatchingThreshold.DialogContentSpecial : config.MatchingThreshold.DialogContentNormal);
            var result = TemplateMatcher.Match(frame, region, template, cachePool);
            if (!result.IsMatch(threshold)) continue;
            var point = ContentToDialogAnchor(new Point(region.X + result.MaxLoc.X, region.Y + result.MaxLoc.Y), font);
            if (_nameTagPosition.IsEmpty) _nameTagPosition = point;
            return point;
        }
        return Point.Empty;
    }

    public int DebugSetFinishedUntilContains(string targetString, string? speaker = null)
    {
        return DebugSetFinishedUntilContains(Set, targetString, speaker);
    }

    private static int DebugSetFinishedUntilContains(IList<DialogBaseFrameSet> set, string targetString,
        string? speaker)
    {
        for (var i = 0; i < set.Count; i++)
        {
            if (set[i].Data.BodyOriginal.Contains(targetString) &&
                (speaker == null || set[i].Data.CharacterOriginal.Contains(speaker)))
                return i;
            set[i].Finished = true;
        }

        return -1;
    }

    public void DebugSetFinishedAfter(int index)
    {
        DebugSetFinishedAfter(Set, index);
    }

    private static void DebugSetFinishedAfter(IList<DialogBaseFrameSet> set, int index)
    {
        for (var i = index; i < set.Count; i++) set[i].Finished = true;
    }

    public bool Process(FrameMatchContext frame, int frameIndex,
        FrameMatchContext? previousFrame = null, int previousFrameIndex = -1)
    {
        MatchStatus? firstStatus = null;

        while (!Finished)
        {
            var dIndex = NextUnfinishedIndex();
            if (dIndex < 0) break;

            ResetForNewTarget(dIndex);

            var dialogRefers = Set[dIndex];
            var useAdaptiveSearch = dialogRefers.IsEmpty() && !IsStatusMatched(_status);
            if (useAdaptiveSearch && !_searchScheduler.ShouldSample(frameIndex))
            {
                _searchScheduler.RememberSkipped(frameIndex);
                return IsStatusMatched(firstStatus ?? MatchStatus.DialogNotMatched);
            }

            var matchResult = MatchForDialog(frame, dialogRefers, frameIndex);
            var currentMatchPoint = matchResult.Point;
            var matchedFrameIndex = frameIndex;
            FrameMatchContext? backcheckFrame = null;
            var backcheckFrameIndex = -1;
            if (useAdaptiveSearch && IsStatusMatched(matchResult.Status) &&
                _searchScheduler.TryGetPrevious(previousFrame, previousFrameIndex,
                    out backcheckFrame, out backcheckFrameIndex))
            {
                var previousResult = MatchForDialog(backcheckFrame!, dialogRefers, backcheckFrameIndex);
                if (IsStatusMatched(previousResult.Status))
                {
                    matchResult = previousResult;
                    matchedFrameIndex = backcheckFrameIndex;
                }
            }

            if (useAdaptiveSearch)
                _searchScheduler.CompleteSample(frameIndex);

            if (!IsStatusMatched(matchResult.Status) && matchResult.Status != MatchStatus.DialogDropped &&
                _lookaheadTracker.ShouldProbe(dIndex, frameIndex, _lookaheadTriggerFrames))
            {
                var originalStatus = _status;
                var originalNameTagPosition = _nameTagPosition;
                var lookaheadEnd = Math.Min(Set.Count, dIndex + MaxLookaheadTargets + 1);
                var foundIndex = -1;
                for (var candidateIndex = dIndex + 1; candidateIndex < lookaheadEnd; candidateIndex++)
                {
                    if (Set[candidateIndex].Finished) continue;
                    _status = MatchStatus.DialogNotMatched;
                    _nameTagPosition = Point.Empty;
                    var candidateResult = MatchForDialog(frame, Set[candidateIndex], frameIndex);
                    if (!IsStatusMatched(candidateResult.Status) ||
                        !ConfirmLookaheadTarget(frame, dIndex, candidateIndex, candidateResult.Point, frameIndex)) continue;

                    foundIndex = candidateIndex;
                    matchResult = candidateResult;
                    currentMatchPoint = candidateResult.Point;
                    matchedFrameIndex = frameIndex;
                    if (backcheckFrame != null)
                    {
                        _status = MatchStatus.DialogNotMatched;
                        _nameTagPosition = Point.Empty;
                        var previousResult = MatchForDialog(backcheckFrame, Set[candidateIndex], backcheckFrameIndex);
                        if (IsStatusMatched(previousResult.Status) &&
                            ConfirmLookaheadTarget(backcheckFrame, dIndex, candidateIndex, previousResult.Point, backcheckFrameIndex))
                        {
                            matchResult = previousResult;
                            matchedFrameIndex = backcheckFrameIndex;
                        }
                    }

                    break;
                }

                if (foundIndex >= 0)
                {
                    for (var missingIndex = dIndex; missingIndex < foundIndex; missingIndex++)
                        if (!Set[missingIndex].Finished)
                            MarkMissing(missingIndex, frameIndex,
                                $"有限前瞻命中对话索引 {foundIndex}，当前目标疑似未出现在视频中");
                    dIndex = foundIndex;
                    dialogRefers = Set[dIndex];
                    _searchScheduler.Reset();
                    _lookaheadTracker.Reset();
                }
                else
                {
                    _status = originalStatus;
                    _nameTagPosition = originalNameTagPosition;
                    _lookaheadTracker.Postpone(frameIndex);
                }
            }

            if (matchResult.Status == MatchStatus.DialogPending &&
                !_trackingGrace.Hold(frameIndex, matchResult.Point))
                matchResult = new MatchResult(matchResult.Point, MatchStatus.DialogDropped);

            _status = matchResult.Status;
            firstStatus ??= matchResult.Status;

            switch (_status)
            {
                case MatchStatus.DialogDropped:
                    _trackingGrace.Clear();
                    MarkDropped(dIndex);
                    _searchScheduler.Reset();
                    _lookaheadTracker.Reset();
                    cachePool.NextDialog();
                    continue;
                case MatchStatus.DialogNotMatched or MatchStatus.NameTagNotMatched:
                    if (TryEnterFallback()) continue;
                    return IsStatusMatched(firstStatus.Value);
                case MatchStatus.DialogPending:
                    // Do not export uncertain frames until full evidence returns.
                    return true;
                default:
                    _trackingGrace.Commit(Set[dIndex]);
                    MatchedFrameRecorder.Record(Set[dIndex], matchedFrameIndex, matchResult.Point,
                        frameIndex, currentMatchPoint);
                    MarkSucceeded();
                    _lookaheadTracker.Reset();
                    return IsStatusMatched(firstStatus.Value);
            }
        }

        return IsStatusMatched(firstStatus ?? MatchStatus.DialogNotMatched);
    }

    private static bool IsStatusMatched(MatchStatus status)
    {
        return status is MatchStatus.DialogMatched1
            or MatchStatus.DialogMatched2
            or MatchStatus.DialogMatched3
            or MatchStatus.DialogPending;
    }

    private MatchResult MatchForDialog(FrameMatchContext frame, DialogBaseFrameSet dialogBase, int frameIndex)
    {
        var lastStatus = _status;
        if (lastStatus is MatchStatus.DialogDropped && dialogBase.IsEmpty())
            lastStatus = MatchStatus.DialogNotMatched;
        Point point;
        if (string.IsNullOrWhiteSpace(dialogBase.Data.CharacterOriginal))
            point = DialogMatchUnnamed(frame, dialogBase);
        else if (dialogBase.Data.Shake || dialogBase.IsEmpty())
            point = DialogMatchNameTag(frame, dialogBase, frameIndex);
        else
            point = dialogBase.Start().Point;

        if (point.IsEmpty)
            return new MatchResult(Point.Empty, IsStatusMatched(lastStatus)
                ? MatchStatus.DialogDropped
                : MatchStatus.NameTagNotMatched);

        return new MatchResult(point, DialogMatchContent(frame, dialogBase, point, lastStatus, frameIndex));
    }

    public DialogMatcherStateDto SaveState()
    {
        var (cf, lfi, uft) = SaveFallbackState();
        return new DialogMatcherStateDto
        {
            SearchState = _searchScheduler.CaptureState(),
            LookaheadState = _lookaheadTracker.CaptureState(),
            PendingTrackingFrames = _trackingGrace.CaptureState(),
            Status = (int)_status,
            ConsecutiveFailures = cf,
            LastFailedIndex = lfi,
            UseFallbackThreshold = uft,
            NameTagPosition = _nameTagPosition.IsEmpty
                ? null
                : new PointDto(_nameTagPosition.X, _nameTagPosition.Y),
            Diagnostics = SaveDiagnostics(),
            FrameSets = Set.Select(d => new DialogFrameSetDto
            {
                Finished = d.Finished,
                UseSeparator = d.SeparatorEnabled,
                SeparateFrame = d.Separate.SeparateFrame,
                SeparatorContentIndex = d.Separate.SeparatorContentIndex,
                Frames = d.Frames.Select(f => new FrameResultDto(f.Index, f.Point.X, f.Point.Y)).ToList()
            }).ToList()
        };
    }

    public void RestoreState(DialogMatcherStateDto state)
    {
        _searchScheduler.RestoreState(state.SearchState);
        _lookaheadTracker.RestoreState(state.LookaheadState);
        _status = (MatchStatus)state.Status;
        _trackingGrace.RestoreState(state.PendingTrackingFrames);
        RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold);
        _nameTagPosition = state.NameTagPosition is { } p
            ? new Point(p.X, p.Y)
            : Point.Empty;
        RestoreDiagnostics(state.Diagnostics);

        for (var i = 0; i < state.FrameSets.Count && i < Set.Count; i++)
        {
            var src = state.FrameSets[i];
            var dst = Set[i];
            dst.Finished = src.Finished;
            dst.SeparatorEnabled = src.UseSeparator;
            dst.SetSeparator(src.SeparateFrame, src.SeparatorContentIndex);
            dst.Frames.Clear();
            foreach (var f in src.Frames)
                dst.Frames.Add(new DialogFrameResult(f.Index, dst.Fps, new Point(f.X, f.Y)));
        }

        NextUnfinishedIndex();
    }

    private enum MatchStatus
    {
        NameTagNotMatched = -2,
        DialogNotMatched = 0,
        DialogMatched1 = 1,
        DialogMatched2 = 2,
        DialogMatched3 = 3,
        DialogPending = 4,
        DialogDropped = -1
    }

    private struct MatchResult(Point point, MatchStatus status)
    {
        public readonly Point Point = point;
        public readonly MatchStatus Status = status;
    }
}

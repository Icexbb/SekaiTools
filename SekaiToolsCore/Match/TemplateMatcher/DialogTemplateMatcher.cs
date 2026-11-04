using System.Drawing;
using SekaiToolsBase;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Process.Performance;
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
    private readonly CompletedFrameSnapshotCache<DialogBaseFrameSet, DialogFrameSetDto> _checkpointFrames = new();

    private readonly Dictionary<string, (GaMat First, GaMat Second, GaMat Third)> _contentTemplates =
        new(StringComparer.Ordinal);

    private readonly TemplateMatchCachePool _lookaheadEvidencePool = new() { Metrics = cachePool.Metrics };
    private readonly TemplateMatchCachePool _transitionEvidencePool = new() { Metrics = cachePool.Metrics };
    private readonly List<FrameResultDto> _pendingNextFrames = [];
    private int? _pendingNextIndex;
    private readonly FiniteLookaheadTracker _lookaheadTracker = new();
    private readonly int _lookaheadTriggerFrames = (int)Math.Ceiling(videoInfo.Fps.Fps() * 3);
    private readonly Dictionary<string, GaMat> _nameTemplates = new(StringComparer.Ordinal);
    private readonly AdaptiveSearchScheduler _searchScheduler = new();
    private readonly DialogTrackingGrace _trackingGrace = new(videoInfo.Fps.Fps());
    private Point _nameTagPosition;
    private MatchStatus _pendingResumeStatus = MatchStatus.DialogMatched3;
    private MatchStatus _status;
    private int? _acquisitionCandidateStart;
    private readonly List<FrameResultDto> _acquisitionCandidateFrames = [];

    public void Dispose()
    {
        _searchScheduler.Dispose();
        _checkpointFrames.Clear();
        _nameTemplates.Clear();
        _contentTemplates.Clear();
        _lookaheadEvidencePool.ResetAll();
        _transitionEvidencePool.ResetAll();
        ClearPendingNext();
        ClearAcquisitionCandidate();
    }

    public int LastNotProcessedIndex()
    {
        return NextUnfinishedIndex();
    }

    private GaMat GetNameTag(string name)
    {
        return templateManager.GetMatchTemplate(TemplateUsage.DialogNameTag, name);
    }

    private Point DialogMatchNameTag(FrameMatchContext frame, DialogBaseFrameSet dialogBase, int frameIndex = -1,
        TemplateMatchCachePool? matchPool = null)
    {
        matchPool ??= cachePool;
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
            var result = TemplateMatcher.Match(src, roi, tmp, matchPool,
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
        int frameIndex = -1, TemplateMatchCachePool? matchPool = null)
    {
        matchPool ??= cachePool;
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
                        !DialogMatchNameTag(frame, dialogBase, frameIndex, matchPool).IsEmpty &&
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
            var result = TemplateMatcher.MatchContent(src, dialogStartPosition, tmp, matchPool, usage,
                effectiveThreshold,
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
    internal static string LookaheadContentPrefix(DialogStoryEvent candidate, IEnumerable<DialogStoryEvent> earlier,
        int maxLength = 12)
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
            if (shared == content.Length || shared >= maxLength) return string.Empty;
            length = Math.Max(length, Math.Min(maxLength, Math.Min(content.Length, shared + 3)));
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
        var threshold = Math.Max(0.85, candidate.Data.Shake
            ? config.MatchingThreshold.DialogContentSpecial
            : config.MatchingThreshold.DialogContentNormal);
        return HasContentEvidence(frame, prefix, point, candidate.IsJitter, frameIndex, threshold) == true;
    }

    private bool? HasContentEvidence(FrameMatchContext frame, string prefix, Point point, bool jitter,
        int frameIndex, double threshold)
    {
        if (prefix.Length == 0 || point.IsEmpty) return null;
        var template = templateManager.GetMatchTemplate(TemplateUsage.DialogContent, prefix);
        var font = TemplateManager.GetFontSize(frame.Size);
        var region = new Rectangle(point.X - (int)(0.5f * font), point.Y + font,
            Math.Max(font * 5, template.Size.Width + font * 2), font * 2);
        if (jitter) region.Extend(1.5);
        region.Limit(new Rectangle(Point.Empty, frame.Size));
        if (region.Width < template.Size.Width || region.Height < template.Size.Height) return null;
        // Independent evidence never calibrates the main tracking pool.
        _lookaheadEvidencePool.ResetAll();
        _lookaheadEvidencePool.SetFrameIndex(frameIndex);
        var result = TemplateMatcher.Match(frame, region, template, _lookaheadEvidencePool);
        return result.IsMatch(threshold);
    }

    private bool ConfirmsSharedPrefixTransition(FrameMatchContext frame, int index, Point point, int frameIndex)
    {
        // Absolute video cadence needs no checkpoint-only timer state. Only the
        // immediate next target is eligible; this is not missing-line lookahead.
        if (frameIndex % Math.Max(1, (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.1)) != 0 || index + 1 >= Set.Count)
            return false;
        var current = Set[index].Data;
        var next = Set[index + 1].Data;
        if (Set[index + 1].Finished ||
            TrimTemplateContent(current.CharacterOriginal) != TrimTemplateContent(next.CharacterOriginal) ||
            current.BodyOriginal.Length < 3 || next.BodyOriginal.Length < 3 ||
            current.BodyOriginal[..3] != next.BodyOriginal[..3])
            return false;
        var nextPrefix = LookaheadContentPrefix(next, [current], 48);
        var currentPrefix = LookaheadContentPrefix(current, [next], 48);
        if (nextPrefix.Length == 0 || currentPrefix.Length == 0) return false;
        using var measurement = cachePool.Metrics.Enter(TemplateMatchingState.Lookahead);
        var nextThreshold = Math.Max(0.90,
            next.Shake ? config.MatchingThreshold.DialogContentSpecial : config.MatchingThreshold.DialogContentNormal);
        var currentThreshold = Math.Max(0.85,
            current.Shake
                ? config.MatchingThreshold.DialogContentSpecial
                : config.MatchingThreshold.DialogContentNormal);
        return HasContentEvidence(frame, nextPrefix, point, Set[index + 1].IsJitter, frameIndex, nextThreshold) ==
               true &&
               HasContentEvidence(frame, currentPrefix, point, Set[index].IsJitter, frameIndex, currentThreshold) ==
               false;
    }

    // Frame sets retain a name-tag-equivalent anchor so tracking, checkpoints and
    // subtitle layout use the same coordinate convention for both dialogue kinds.
    internal static Point ContentToDialogAnchor(Point contentPosition, int fontSize)
    {
        return new Point(contentPosition.X + (int)(0.5f * fontSize), contentPosition.Y - fontSize);
    }

    private Point DialogMatchUnnamed(FrameMatchContext frame, DialogBaseFrameSet dialog,
        TemplateMatchCachePool? matchPool = null)
    {
        matchPool ??= cachePool;
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
                dialog.Data.Shake
                    ? config.MatchingThreshold.DialogContentSpecial
                    : config.MatchingThreshold.DialogContentNormal);
            var result = TemplateMatcher.Match(frame, region, template, matchPool);
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

            var matchedWithFallback = IsUsingFallback;
            var matchResult = MatchForDialog(frame, dialogRefers, frameIndex);
            if (_status == MatchStatus.DialogMatched3 && matchResult.Status == MatchStatus.DialogMatched3 &&
                ConfirmsSharedPrefixTransition(frame, dIndex, matchResult.Point, frameIndex))
            {
                Logger.Log($"共享前缀区分文本确认相邻对话切换: {dIndex} -> {dIndex + 1}, 帧={frameIndex}");
                matchResult = new MatchResult(matchResult.Point, MatchStatus.DialogDropped);
            }

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
                _lookaheadTracker.ShouldProbe(dIndex, frameIndex, _lookaheadTriggerFrames,
                    VisualChangeEvidence.Sample(frame, EvidenceRegion.Dialog)))
            {
                using var measurement = cachePool.Metrics.Enter(TemplateMatchingState.Lookahead);
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
                        !ConfirmLookaheadTarget(frame, dIndex, candidateIndex, candidateResult.Point, frameIndex))
                        continue;

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
                            ConfirmLookaheadTarget(backcheckFrame, dIndex, candidateIndex, previousResult.Point,
                                backcheckFrameIndex))
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

            var beginCandidate = _acquisitionCandidateStart == null && dialogRefers.IsEmpty() &&
                                 matchedWithFallback && IsStatusMatched(matchResult.Status) &&
                                 matchResult.Status != MatchStatus.DialogPending;
            if (beginCandidate)
            {
                // Fallback may also find a perfectly strong first glyph. Recheck
                // normally before withholding it, including the sampled backcheck.
                MarkSucceeded();
                var normalResult = MatchForDialog(frame, dialogRefers, frameIndex);
                if (IsStatusMatched(normalResult.Status))
                {
                    beginCandidate = false;
                    matchResult = normalResult;
                    currentMatchPoint = normalResult.Point;
                    matchedFrameIndex = frameIndex;
                    if (backcheckFrame != null)
                    {
                        var normalPrevious = MatchForDialog(backcheckFrame, dialogRefers, backcheckFrameIndex);
                        if (IsStatusMatched(normalPrevious.Status))
                        {
                            matchResult = normalPrevious;
                            matchedFrameIndex = backcheckFrameIndex;
                        }
                    }
                }
            }
            if (beginCandidate) _acquisitionCandidateStart = matchedFrameIndex;

            if (_acquisitionCandidateStart == null && matchResult.Status == MatchStatus.DialogPending)
                RememberNextDuringLoss(frame, dIndex, frameIndex);

            if (matchResult.Status == MatchStatus.DialogPending &&
                !(matchResult.Point.IsEmpty
                    ? _trackingGrace.HoldUnlocated(frameIndex)
                    : _trackingGrace.Hold(frameIndex, matchResult.Point)))
                matchResult = new MatchResult(matchResult.Point, MatchStatus.DialogDropped);

            // A weak opening glyph has not identified a dialogue yet. Losing it
            // must retry this target, rather than consume an entire script event.
            if (_acquisitionCandidateStart is { } candidateStart &&
                (matchResult.Status == MatchStatus.DialogDropped || frameIndex - candidateStart >= videoInfo.Fps.Fps() * 2))
            {
                ClearAcquisitionCandidate();
                _trackingGrace.Clear();
                ClearPendingNext();
                _status = MatchStatus.DialogNotMatched;
                _pendingResumeStatus = MatchStatus.DialogMatched3;
                _nameTagPosition = Point.Empty;
                _searchScheduler.Reset();
                MarkSucceeded();
                return false;
            }

            if (matchResult.Status == MatchStatus.DialogPending && _status != MatchStatus.DialogPending)
                _pendingResumeStatus = _status;
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
                    ClearPendingNext();
                    if (TryEnterFallback(frameIndex)) continue;
                    return IsStatusMatched(firstStatus.Value);
                case MatchStatus.DialogPending:
                    // Do not export uncertain frames until full evidence returns.
                    return true;
                default:
                    if (_acquisitionCandidateStart != null)
                    {
                        // The first fallback hit is never exported. Only later
                        // normal-threshold evidence may confirm and populate it.
                        _trackingGrace.Clear();
                        if (!beginCandidate)
                        {
                            _acquisitionCandidateFrames.Add(new FrameResultDto(matchedFrameIndex,
                                matchResult.Point.X, matchResult.Point.Y));
                            if (matchedFrameIndex < frameIndex)
                                _acquisitionCandidateFrames.Add(new FrameResultDto(frameIndex,
                                    currentMatchPoint.X, currentMatchPoint.Y));
                        }
                        var shortConfirmed = dialogRefers.Data.BodyOriginal.Length < 3 &&
                                             _acquisitionCandidateFrames.Count >= 2 &&
                                             _acquisitionCandidateFrames[^1].Index ==
                                             _acquisitionCandidateFrames[^2].Index + 1;
                        if (matchResult.Status == MatchStatus.DialogMatched3 || shortConfirmed)
                        {
                            foreach (var evidence in _acquisitionCandidateFrames)
                                dialogRefers.Add(evidence.Index, new Point(evidence.X, evidence.Y));
                            ClearAcquisitionCandidate();
                            _lookaheadTracker.Reset();
                        }
                        MarkSucceeded();
                        return true;
                    }
                    CommitPendingNext(dIndex, matchedFrameIndex);
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

    // Keep only bounded, independently matched coordinates while disappearance is
    // uncertain. No image buffers or speculative frames enter the exported sets.
    private void RememberNextDuringLoss(FrameMatchContext frame, int currentIndex, int frameIndex)
    {
        var nextIndex = currentIndex + 1;
        if (nextIndex >= Set.Count || Set[nextIndex].Finished || !Set[nextIndex].IsEmpty())
        {
            ClearPendingNext();
            return;
        }

        var current = Set[currentIndex].Data;
        var candidate = Set[nextIndex].Data;
        // Shared opening glyphs cannot prove when a same-speaker line changed.
        // Keep the existing distinguishing-prefix path for those transitions.
        if (TrimTemplateContent(current.CharacterOriginal) == TrimTemplateContent(candidate.CharacterOriginal) &&
            (current.BodyOriginal.Length == 0 || candidate.BodyOriginal.Length == 0 ||
             current.BodyOriginal[0] == candidate.BodyOriginal[0]))
        {
            ClearPendingNext();
            return;
        }

        var savedStatus = _status;
        var savedPosition = _nameTagPosition;
        try
        {
            _status = MatchStatus.DialogNotMatched;
            _transitionEvidencePool.ResetAll();
            _transitionEvidencePool.SetFrameIndex(frameIndex);
            var next = Set[nextIndex];
            var point = string.IsNullOrWhiteSpace(next.Data.CharacterOriginal)
                ? DialogMatchUnnamed(frame, next, _transitionEvidencePool)
                : DialogMatchNameTag(frame, next, frameIndex, _transitionEvidencePool);
            var status = DialogMatchContent(frame, next, point, MatchStatus.DialogNotMatched,
                frameIndex, _transitionEvidencePool);
            if (!IsStatusMatched(status))
            {
                ClearPendingNext();
                return;
            }

            if (_pendingNextIndex != nextIndex ||
                (_pendingNextFrames.Count > 0 && _pendingNextFrames[^1].Index != frameIndex - 1))
                ClearPendingNext();
            _pendingNextIndex = nextIndex;
            _pendingNextFrames.Add(new FrameResultDto(frameIndex, point.X, point.Y));
            var limit = Math.Max(1, (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.05)) + 1;
            if (_pendingNextFrames.Count > limit) _pendingNextFrames.RemoveAt(0);
        }
        finally
        {
            _status = savedStatus;
            _nameTagPosition = savedPosition;
        }
    }

    private void CommitPendingNext(int index, int selectedFrame)
    {
        if (_pendingNextIndex == index && _pendingNextFrames.Count > 0 &&
            _pendingNextFrames[^1].Index >= selectedFrame - 1)
            foreach (var frame in _pendingNextFrames)
                if (frame.Index < selectedFrame)
                    Set[index].Add(frame.Index, new Point(frame.X, frame.Y));
        ClearPendingNext();
    }

    private void ClearPendingNext()
    {
        _pendingNextFrames.Clear();
        _pendingNextIndex = null;
    }

    private void ClearAcquisitionCandidate()
    {
        _acquisitionCandidateStart = null;
        _acquisitionCandidateFrames.Clear();
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
        using var measurement = cachePool.Metrics.Enter(
            cachePool.Metrics.CurrentState == TemplateMatchingState.Lookahead
                ? TemplateMatchingState.Lookahead
                : IsUsingFallback || _status == MatchStatus.DialogPending
                    ? TemplateMatchingState.Recovering
                    : _status == MatchStatus.DialogMatched3
                        ? TemplateMatchingState.Tracking
                        : IsStatusMatched(_status)
                            ? TemplateMatchingState.Acquiring
                            : TemplateMatchingState.Searching);
        var lastStatus = _status == MatchStatus.DialogPending ? _pendingResumeStatus : _status;
        if (dialogBase.IsEmpty() && lastStatus is MatchStatus.DialogDropped or MatchStatus.NameTagNotMatched)
            lastStatus = MatchStatus.DialogNotMatched;
        Point point;
        if (string.IsNullOrWhiteSpace(dialogBase.Data.CharacterOriginal))
            point = DialogMatchUnnamed(frame, dialogBase);
        else if (dialogBase.Data.Shake || dialogBase.IsEmpty())
            point = DialogMatchNameTag(frame, dialogBase, frameIndex);
        else
            point = dialogBase.Start().Point;

        if (point.IsEmpty)
        {
            // Losing the name anchor is uncertain disappearance, not a confirmed
            // end. Keep no estimated coordinates for these frames.
            if (IsStatusMatched(lastStatus))
                return new MatchResult(Point.Empty, MatchStatus.DialogPending);
            return new MatchResult(Point.Empty, IsStatusMatched(lastStatus)
                ? MatchStatus.DialogDropped
                : MatchStatus.NameTagNotMatched);
        }

        var result = DialogMatchContent(frame, dialogBase, point, lastStatus, frameIndex);
        if (result == MatchStatus.DialogDropped &&
            lastStatus is MatchStatus.DialogMatched1 or MatchStatus.DialogMatched2)
            return new MatchResult(Point.Empty, MatchStatus.DialogPending);
        return new MatchResult(point, result);
    }

    public DialogMatcherStateDto SaveState()
    {
        return SaveState(false);
    }

    internal DialogMatcherStateDto SaveState(bool checkpoint)
    {
        if (!checkpoint) _checkpointFrames.Clear();
        var (cf, lfi, uft) = SaveFallbackState();
        return new DialogMatcherStateDto
        {
            AcquisitionCandidateStartFrame = _acquisitionCandidateStart,
            AcquisitionCandidateFrames = [.. _acquisitionCandidateFrames],
            SearchState = _searchScheduler.CaptureState(),
            LookaheadState = _lookaheadTracker.CaptureState(),
            PendingTrackingFrames = _trackingGrace.CaptureState(),
            PendingNextDialogIndex = _pendingNextIndex,
            PendingNextDialogFrames = [.. _pendingNextFrames],
            Status = (int)_status,
            PendingResumeStatus =
                (int)(_status == MatchStatus.DialogPending ? _pendingResumeStatus : MatchStatus.DialogMatched3),
            ConsecutiveFailures = cf,
            LastFailureFrame = LastFailureFrame,
            LastFailedIndex = lfi,
            UseFallbackThreshold = uft,
            NameTagPosition = _nameTagPosition.IsEmpty
                ? null
                : new PointDto(_nameTagPosition.X, _nameTagPosition.Y),
            Diagnostics = SaveDiagnostics(),
            FrameSets = Set.Select(d => _checkpointFrames.Capture(d, d.Finished, checkpoint, d => new DialogFrameSetDto
            {
                Finished = d.Finished,
                UseSeparator = d.SeparatorEnabled,
                SeparateFrame = d.Separate.SeparateFrame,
                SeparatorContentIndex = d.Separate.SeparatorContentIndex,
                Frames = d.Frames.Select(f => new FrameResultDto(f.Index, f.Point.X, f.Point.Y)).ToList()
            })).ToList()
        };
    }

    public void RestoreState(DialogMatcherStateDto state)
    {
        _checkpointFrames.Clear();
        if (state.PendingResumeStatus is < 1 or > 3)
            throw new InvalidDataException("Invalid dialogue pending resume stage");

        _searchScheduler.RestoreState(state.SearchState);
        _lookaheadTracker.RestoreState(state.LookaheadState);
        _status = (MatchStatus)state.Status;
        _pendingResumeStatus = (MatchStatus)state.PendingResumeStatus;
        _trackingGrace.RestoreState(state.PendingTrackingFrames);
        ClearAcquisitionCandidate();
        if (state.AcquisitionCandidateStartFrame is { } candidateStart)
        {
            if (candidateStart < 0 || state.Status is not (1 or 2 or 4) ||
                state.AcquisitionCandidateFrames.Count > Math.Ceiling(videoInfo.Fps.Fps() * 2) + 1 ||
                state.AcquisitionCandidateFrames.Any(item => item.Index < candidateStart))
                throw new InvalidDataException("Invalid dialogue acquisition candidate");
            _acquisitionCandidateStart = candidateStart;
            _acquisitionCandidateFrames.AddRange(state.AcquisitionCandidateFrames);
        }
        ClearPendingNext();
        if (state.PendingNextDialogIndex is { } nextIndex && nextIndex >= 0 && nextIndex < Set.Count &&
            state.Status == (int)MatchStatus.DialogPending)
        {
            _pendingNextIndex = nextIndex;
            _pendingNextFrames.AddRange(state.PendingNextDialogFrames);
        }
        RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold,
            state.LastFailureFrame);
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

using System.Drawing;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Process.Performance;
using SekaiToolsCore.Utils;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class BannerTemplateMatcher(
    VideoInfo videoInfo,
    SekaiStory storyData,
    TemplateManager templateManager,
    TemplateMatchCachePool cachePool,
    Config config
) : MatcherStateMachine<BannerBaseFrameSet>(
    storyData.Banners().Select(d => new BannerBaseFrameSet(d, videoInfo.Fps)).ToList(),
    (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5)
), IDisposable
{
    private const int MaxLookaheadTargets = 3;
    private readonly CompletedFrameSnapshotCache<BannerBaseFrameSet, BannerFrameSetDto> _checkpointFrames = new();
    private readonly FiniteLookaheadTracker _lookaheadTracker = new();
    private readonly int _lookaheadTriggerFrames = (int)Math.Ceiling(videoInfo.Fps.Fps() * 3);
    private readonly AdaptiveSearchScheduler _searchScheduler = new();
    private readonly TrackingLossWindow _trackingLoss = new(videoInfo.Fps.Fps());
    private MatchStatus _status;
    internal bool IsTracking => _status == MatchStatus.Matched;

    public void Dispose()
    {
        _checkpointFrames.Clear();
        _searchScheduler.Dispose();
    }

    internal bool Probe(FrameMatchContext frame)
    {
        var index = LastNotProcessedIndex();
        if (index < 0) return false;
        var text = Set[index].Data.BodyOriginal;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var template = GetTemplate(text);
        var region = UtilFunc.FromCenter(frame.Size.Center(),
            new Size(template.Size.Width * 2, template.Size.Height * 2));
        return IndependentEventProbe.Confirm(frame, template, region, cachePool.Metrics,
            config.MatchingThreshold.BannerNormal);
    }

    public int LastNotProcessedIndex()
    {
        return NextUnfinishedIndex();
    }

    private GaMat GetTemplate(string content)
    {
        return templateManager.GetMatchTemplate(TemplateUsage.BannerContent, content);
    }

    internal static string TrimContent(string content)
    {
        var trimmed = "";
        var len = 0D;
        foreach (var c in content)
        {
            trimmed += c;
            len += char.IsAscii(c) ? 0.5 : 1;
            if (len >= 5) break;
        }

        foreach (var c in new[] { '・', '　', ' ' })
            if (trimmed.Contains(c))
                trimmed = trimmed[..trimmed.IndexOf(c)];

        return trimmed;
    }

    private MatchStatus BannerMatch(FrameMatchContext frame, string text, int frameIndex = -1)
    {
        using var measurement = cachePool.Metrics.Enter(
            cachePool.Metrics.CurrentState == TemplateMatchingState.Lookahead
                ? TemplateMatchingState.Lookahead
                : IsUsingFallback
                    ? TemplateMatchingState.Recovering
                    : _status == MatchStatus.Matched
                        ? TemplateMatchingState.Tracking
                        : TemplateMatchingState.Searching);
        if (string.IsNullOrWhiteSpace(text)) return MatchStatus.NotMatched;
        var sText = TrimContent(text);
        if (string.IsNullOrWhiteSpace(sText)) return MatchStatus.NotMatched;
        var template = GetTemplate(sText);
        var match = LocalMatch(frame, template);

        return _status switch
        {
            MatchStatus.Matched => match ? MatchStatus.Matched : MatchStatus.Dropped,
            MatchStatus.NotMatched or MatchStatus.Dropped => match ? MatchStatus.Matched : MatchStatus.NotMatched,
            _ => throw new ArgumentOutOfRangeException(nameof(_status), _status, null)
        };

        bool LocalMatch(FrameMatchContext src, GaMat tmp)
        {
            var cropArea = UtilFunc.FromCenter(src.Size.Center(),
                new Size((int)(tmp.Size.Height * text.Length * 1.5), (int)(tmp.Size.Height * 1.5)));
            cropArea.Limit(new Rectangle(Point.Empty, src.Size));
            if (cropArea.Width < tmp.Size.Width || cropArea.Height < tmp.Size.Height)
                return false;
            var result = TemplateMatcher.Match(src, cropArea, tmp, cachePool,
                TemplateMatchCachePool.MatchUsage.Banner);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(BannerTemplateMatcher)} Frame {frameIndex} Match Banner {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);

            return result.IsMatch(EffectiveThreshold(config.MatchingThreshold.BannerNormal,
                _status == MatchStatus.Matched));
        }
    }

    public void Process(FrameMatchContext frame, int frameIndex,
        FrameMatchContext? previousFrame = null, int previousFrameIndex = -1)
    {
        while (!Finished)
        {
            var index = NextUnfinishedIndex();
            if (index < 0) return;

            ResetForNewTarget(index);

            var useAdaptiveSearch = _status != MatchStatus.Matched && Set[index].IsEmpty();
            if (useAdaptiveSearch && !_searchScheduler.ShouldSample(frameIndex))
            {
                _searchScheduler.RememberSkipped(frameIndex);
                return;
            }

            var matchResult = BannerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
            var matchedFrameIndex = frameIndex;
            FrameMatchContext? backcheckFrame = null;
            var backcheckFrameIndex = -1;
            if (useAdaptiveSearch && matchResult == MatchStatus.Matched &&
                _searchScheduler.TryGetPrevious(previousFrame, previousFrameIndex,
                    out backcheckFrame, out backcheckFrameIndex))
            {
                var previousResult = BannerMatch(backcheckFrame!, Set[index].Data.BodyOriginal,
                    backcheckFrameIndex);
                if (previousResult == MatchStatus.Matched)
                {
                    matchResult = previousResult;
                    matchedFrameIndex = backcheckFrameIndex;
                }
            }

            if (useAdaptiveSearch)
                _searchScheduler.CompleteSample(frameIndex);

            if (matchResult == MatchStatus.NotMatched &&
                _lookaheadTracker.ShouldProbe(index, frameIndex, _lookaheadTriggerFrames,
                    VisualChangeEvidence.Sample(frame, EvidenceRegion.Banner)))
            {
                using var measurement = cachePool.Metrics.Enter(TemplateMatchingState.Lookahead);
                var originalStatus = _status;
                var lookaheadEnd = Math.Min(Set.Count, index + MaxLookaheadTargets + 1);
                var foundIndex = -1;
                for (var candidateIndex = index + 1; candidateIndex < lookaheadEnd; candidateIndex++)
                {
                    if (Set[candidateIndex].Finished) continue;
                    _status = MatchStatus.NotMatched;
                    var candidateResult = BannerMatch(frame, Set[candidateIndex].Data.BodyOriginal, frameIndex);
                    if (candidateResult != MatchStatus.Matched) continue;

                    foundIndex = candidateIndex;
                    matchResult = candidateResult;
                    matchedFrameIndex = frameIndex;
                    if (backcheckFrame != null)
                    {
                        _status = MatchStatus.NotMatched;
                        var previousResult = BannerMatch(backcheckFrame,
                            Set[candidateIndex].Data.BodyOriginal, backcheckFrameIndex);
                        if (previousResult == MatchStatus.Matched)
                        {
                            matchResult = previousResult;
                            matchedFrameIndex = backcheckFrameIndex;
                        }
                    }

                    break;
                }

                if (foundIndex >= 0)
                {
                    for (var missingIndex = index; missingIndex < foundIndex; missingIndex++)
                        if (!Set[missingIndex].Finished)
                            MarkMissing(missingIndex, frameIndex,
                                $"有限前瞻命中横幅索引 {foundIndex}，当前目标疑似未出现在视频中");
                    index = foundIndex;
                    _searchScheduler.Reset();
                    _lookaheadTracker.Reset();
                }
                else
                {
                    _status = originalStatus;
                    _lookaheadTracker.Postpone(frameIndex);
                }
            }

            if (matchResult == MatchStatus.Dropped && _trackingLoss.ShouldWait(frameIndex))
                return;
            if (matchResult is MatchStatus.Matched or MatchStatus.Dropped)
                _trackingLoss.Clear();

            _status = matchResult;
            switch (matchResult)
            {
                case MatchStatus.Dropped:
                    MarkDropped(index);
                    _searchScheduler.Reset();
                    _lookaheadTracker.Reset();
                    continue;
                case MatchStatus.NotMatched:
                    if (TryEnterFallback(frameIndex)) continue;
                    return;
                case MatchStatus.Matched:
                default:
                    MatchedFrameRecorder.Record(Set[index], matchedFrameIndex, frameIndex);
                    MarkSucceeded();
                    _lookaheadTracker.Reset();
                    return;
            }
        }
    }

    public BannerMatcherStateDto SaveState()
    {
        return SaveState(false);
    }

    internal BannerMatcherStateDto SaveState(bool checkpoint)
    {
        if (!checkpoint) _checkpointFrames.Clear();
        var (cf, lfi, uft) = SaveFallbackState();
        return new BannerMatcherStateDto
        {
            SearchState = _searchScheduler.CaptureState(),
            LookaheadState = _lookaheadTracker.CaptureState(),
            Status = (int)_status,
            PendingLossFrame = _trackingLoss.FirstMissingFrame,
            ConsecutiveFailures = cf,
            LastFailureFrame = LastFailureFrame,
            LastFailedIndex = lfi,
            UseFallbackThreshold = uft,
            Diagnostics = SaveDiagnostics(),
            FrameSets = Set.Select(b => _checkpointFrames.Capture(b, b.Finished, checkpoint, b => new BannerFrameSetDto
            {
                Finished = b.Finished,
                Start = b.IsEmpty() ? -1 : b.StartIndex(),
                End = b.IsEmpty() ? -1 : b.EndIndex()
            })).ToList()
        };
    }

    public void RestoreState(BannerMatcherStateDto state)
    {
        _checkpointFrames.Clear();
        _searchScheduler.RestoreState(state.SearchState);
        _lookaheadTracker.RestoreState(state.LookaheadState);
        _status = (MatchStatus)state.Status;
        _trackingLoss.Restore(state.PendingLossFrame);
        RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold,
            state.LastFailureFrame);
        RestoreDiagnostics(state.Diagnostics);

        for (var i = 0; i < state.FrameSets.Count && i < Set.Count; i++)
        {
            var src = state.FrameSets[i];
            var dst = Set[i];
            dst.Finished = src.Finished;
            if (src.Start >= 0 && src.End >= 0)
                dst.SetFrameRange(src.Start, src.End);
        }

        NextUnfinishedIndex();
    }

    private enum MatchStatus
    {
        NotMatched,
        Matched,
        Dropped
    }
}
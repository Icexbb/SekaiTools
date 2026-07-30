using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class MarkerTemplateMatcher(
    VideoInfo videoInfo,
    SekaiStory storyData,
    TemplateManager templateManager,
    TemplateMatchCachePool cachePool,
    Config config
) : MatcherStateMachine<MarkerBaseFrameSet>(
    storyData.Markers().Select(d => new MarkerBaseFrameSet(d, videoInfo.Fps)).ToList(),
    (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5)
), IDisposable
{
    private readonly Dictionary<string, GaMat> _templates = new();
    private MatchStatus _status;

    public void Dispose()
    {
        foreach (var template in _templates.Values)
            template.Dispose();
        _templates.Clear();
    }

    public int LastNotProcessedIndex()
    {
        return NextUnfinishedIndex();
    }

    private GaMat GetTemplate(string content)
    {
        if (_templates.TryGetValue(content, out var template))
            return template;

        var source = templateManager.GetTemplate(TemplateUsage.MarkerContent, content);
        const double resizeRatio = 0.90;
        using var resized = new Mat();
        CvInvoke.Resize(source, resized,
            new Size((int)(source.Width * resizeRatio), (int)(source.Height * resizeRatio)));
        _templates.Add(content, new GaMat(resized));
        return _templates[content];
    }

    private MatchResult MarkerMatch(Mat img, string text, int frameIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new MatchResult(Point.Empty, MatchStatus.NotMatched);

        var templateAll = GetTemplate(text);
        var sText = text[^1].ToString();
        var template = GetTemplate(sText);
        var matchedPoint = LocalMatch(img, template, TemplateMatchingType.CcoeffNormed);

        return _status switch
        {
            MatchStatus.Matched => new MatchResult(matchedPoint,
                matchedPoint.IsEmpty ? MatchStatus.Dropped : MatchStatus.Matched),
            MatchStatus.NotMatched or MatchStatus.Dropped => new MatchResult(matchedPoint,
                matchedPoint.IsEmpty ? MatchStatus.NotMatched : MatchStatus.Matched),
            _ => throw new ArgumentOutOfRangeException(nameof(_status), _status, null)
        };

        Point LocalMatch(Mat src, GaMat tmp, TemplateMatchingType matchingType)
        {
            var cropArea = new Rectangle(Point.Empty,
                new Size((int)(templateAll.Size.Width * 1.5), (int)(tmp.Size.Height * 3.0)));
            if (cropArea.Width < tmp.Size.Width || cropArea.Height < tmp.Size.Height)
                return Point.Empty;

            if (_status == MatchStatus.Matched)
                cropArea.Width = Math.Min(cropArea.Width * 2, src.Width - cropArea.X);

            cropArea.Limit(new Rectangle(Point.Empty, src.Size));
            if (cropArea.IsEmpty)
                return Point.Empty;

            if (cropArea.Width < tmp.Size.Width || cropArea.Height < tmp.Size.Height)
                return Point.Empty;

            using var imgCropped = new Mat(src, cropArea);
            var matchResult =
                TemplateMatcher.Match(imgCropped, tmp, cachePool,
                    TemplateMatchCachePool.MatchUsage.Marker, matchingType);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Marker {LastNotProcessedIndex()} Result: {matchResult.MaxVal}",
                    ExtLogLevel.Debug);

            var effectiveThreshold = EffectiveThreshold(config.MatchingThreshold.MarkerNormal);
            var matched = matchResult.MaxVal > effectiveThreshold && matchResult.MaxVal < 1;

            if (!matched) return Point.Empty;

            var result = matchResult.MaxLoc + new Size(cropArea.X, cropArea.Y) - templateAll.Size + tmp.Size;
            return result;
        }
    }

    public void Process(Mat frame, int frameIndex)
    {
        while (!Finished)
        {
            var index = NextUnfinishedIndex();
            if (index < 0) return;

            ResetForNewTarget(index);

            var matchResult = MarkerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
            _status = matchResult.Status;

            switch (matchResult.Status)
            {
                case MatchStatus.Dropped:
                    MarkDropped(index);
                    continue;
                case MatchStatus.NotMatched:
                    if (TryEnterFallback()) continue;
                    return;
                case MatchStatus.Matched:
                default:
                    Set[index].Add(frameIndex, matchResult.Point);
                    MarkSucceeded();
                    return;
            }
        }
    }

    public MarkerMatcherStateDto SaveState()
    {
        var (cf, lfi, uft) = SaveFallbackState();
        return new MarkerMatcherStateDto
        {
            Status = (int)_status,
            ConsecutiveFailures = cf,
            LastFailedIndex = lfi,
            UseFallbackThreshold = uft,
            FrameSets = Set.Select(m => new MarkerFrameSetDto
            {
                Finished = m.Finished,
                Frames = m.Frames.Select(f => new FrameResultDto(f.Index, f.Point.X, f.Point.Y)).ToList()
            }).ToList()
        };
    }

    public void RestoreState(MarkerMatcherStateDto state)
    {
        _status = (MatchStatus)state.Status;
        RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold);

        for (var i = 0; i < state.FrameSets.Count && i < Set.Count; i++)
        {
            var src = state.FrameSets[i];
            var dst = Set[i];
            dst.Finished = src.Finished;
            dst.Frames.Clear();
            foreach (var f in src.Frames)
                dst.Frames.Add(new MarkerFrameResult(f.Index, dst.Fps, new Point(f.X, f.Y)));
        }

        NextUnfinishedIndex();
    }

    private enum MatchStatus
    {
        NotMatched,
        Dropped,
        Matched
    }

    private struct MatchResult(Point point, MatchStatus status)
    {
        public readonly Point Point = point;
        public readonly MatchStatus Status = status;
    }
}

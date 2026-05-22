using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiStory = SekaiToolsBase.Story.Story;


namespace SekaiToolsCore.Match.TemplateMatcher;

public class MarkerTemplateMatcher(
    VideoInfo videoInfo,
    SekaiStory storyData,
    TemplateManager templateManager,
    Config config)
{
    private readonly Dictionary<string, GaMat> _templates = new();

    public readonly List<MarkerBaseFrameSet> Set = storyData.Markers()
        .Select(d => new MarkerBaseFrameSet(d, videoInfo.Fps))
        .ToList();

    private MatchStatus _status;

    private int _consecutiveFailures;
    private int _lastFailedIndex = -1;
    private bool _useFallbackThreshold;
    private const double FallbackRatio = 0.7;
    private const double AbsMinThreshold = 0.40;
    private int FallbackTriggerFrames => (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5);

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;

    private GaMat GetTemplate(string content)
    {
        if (_templates.TryGetValue(content, out var template))
            return template;

        var mat = templateManager.GetTemplate(TemplateUsage.MarkerContent, content);
        const double resizeRatio = 0.90;
        CvInvoke.Resize(mat, mat, new Size((int)(mat.Width * resizeRatio), (int)(mat.Height * resizeRatio)));
        _templates.Add(content, new GaMat(mat));
        return _templates[content];
    }

    private MatchResult MarkerMatch(Mat img, string text, int frameIndex = -1)
    {
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

            if (cropArea.IsEmpty)
                return Point.Empty;

            using var imgCropped = new Mat(src, cropArea);
            var matchResult =
                TemplateMatcher.Match(imgCropped, tmp, TemplateMatchCachePool.MatchUsage.Marker, matchingType);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Marker {LastNotProcessedIndex()} Result: {matchResult.MaxVal}",
                    ExtLogLevel.Debug);

            var threshold = config.MatchingThreshold.MarkerNormal;
            var effectiveThreshold = _useFallbackThreshold
                ? Math.Max(threshold * FallbackRatio, AbsMinThreshold)
                : threshold;
            var matched = matchResult.MaxVal > effectiveThreshold && matchResult.MaxVal < 1;

            if (!matched) return Point.Empty;

            var result = matchResult.MaxLoc + new Size(cropArea.X, cropArea.Y) - templateAll.Size + tmp.Size;
            return result;
        }
    }

    private int _firstUnfinishedIndex;

    private void AdvanceFirstUnfinished()
    {
        while (_firstUnfinishedIndex < Set.Count && Set[_firstUnfinishedIndex].Finished)
            _firstUnfinishedIndex++;
    }

    private static int LastNotProcessedIndex(IReadOnlyList<MarkerBaseFrameSet> set)
    {
        for (var i = 0; i < set.Count; i++)
            if (!set[i].Finished)
                return i;
        return -1;
    }

    public int LastNotProcessedIndex()
    {
        AdvanceFirstUnfinished();
        return _firstUnfinishedIndex < Set.Count ? _firstUnfinishedIndex : -1;
    }

    public void Process(Mat frame, int frameIndex)
    {
        while (!Finished)
        {
            var index = LastNotProcessedIndex();
            if (index < 0) return;

            if (index != _lastFailedIndex)
            {
                _lastFailedIndex = index;
                _consecutiveFailures = 0;
                _useFallbackThreshold = false;
            }

            var matchResult = MarkerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
            _status = matchResult.Status;

            switch (matchResult.Status)
            {
                case MatchStatus.Dropped:
                    Set[index].Finished = true;
                    _consecutiveFailures = 0;
                    _useFallbackThreshold = false;
                    continue;
                case MatchStatus.NotMatched:
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= FallbackTriggerFrames && !_useFallbackThreshold)
                    {
                        _useFallbackThreshold = true;
                        _consecutiveFailures = 0;
                        continue;
                    }
                    _useFallbackThreshold = false;
                    return;
                case MatchStatus.Matched:
                default:
                    Set[index].Add(frameIndex, matchResult.Point);
                    _consecutiveFailures = 0;
                    _useFallbackThreshold = false;
                    return;
            }
        }
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

    public MarkerMatcherStateDto SaveState()
    {
        return new MarkerMatcherStateDto
        {
            Status = (int)_status,
            ConsecutiveFailures = _consecutiveFailures,
            LastFailedIndex = _lastFailedIndex,
            UseFallbackThreshold = _useFallbackThreshold,
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
        _consecutiveFailures = state.ConsecutiveFailures;
        _lastFailedIndex = state.LastFailedIndex;
        _useFallbackThreshold = state.UseFallbackThreshold;

        for (var i = 0; i < state.FrameSets.Count && i < Set.Count; i++)
        {
            var src = state.FrameSets[i];
            var dst = Set[i];
            dst.Finished = src.Finished;
            dst.Frames.Clear();
            foreach (var f in src.Frames)
                dst.Frames.Add(new MarkerFrameResult(f.Index, dst.Fps, new Point(f.X, f.Y)));
        }

        _firstUnfinishedIndex = 0;
        AdvanceFirstUnfinished();
    }
}
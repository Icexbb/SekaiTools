using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiStory = SekaiToolsBase.Story.Story;


namespace SekaiToolsCore.Match.TemplateMatcher;

public class MarkerTemplateMatcher(VideoInfo videoInfo, SekaiStory storyData, TemplateManager templateManager, Config config)
{
    private readonly Dictionary<string, GaMat> _templates = new();

    public readonly List<MarkerBaseFrameSet> Set = storyData.Markers().Select(d => new MarkerBaseFrameSet(d, videoInfo.Fps))
        .ToList();

    private MatchStatus _status;

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

        Point LocalMatch(Mat src, GaMat tmp, TemplateMatchingType matchingType, Point startPos = default)
        {
            var cropArea = GetRectNormal(tmp, startPos);
            if (cropArea.IsEmpty)
                return Point.Empty;

            var imgCropped = new Mat(src, cropArea);
            var matchResult =
                TemplateMatcher.Match(imgCropped, tmp, TemplateMatchCachePool.MatchUsage.Marker, matchingType);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Marker {LastNotProcessedIndex()} Result: {matchResult.MaxVal}");

            var matched = matchResult.MaxVal > config.MatchingThreshold.MarkerNormal && matchResult.MaxVal < 1;

            if (!matched) return Point.Empty;

            var result = matchResult.MaxLoc + new Size(cropArea.X, cropArea.Y) - templateAll.Size + tmp.Size;

            if (_status != MatchStatus.Matched) return result;

            var resultRight = LocalMatch(src, tmp, matchingType,
                new Point(cropArea.Left + matchResult.MaxLoc.X + tmp.Size.Width, 0));

            return resultRight.IsEmpty ? result : resultRight;
        }

        Rectangle GetRectNormal(GaMat tmp, Point startPos)
        {
            var size = new Size(
                (int)(templateAll.Size.Width * 1.5) - startPos.X,
                (int)(tmp.Size.Height * 3.0)
            );
            if (size.Width <= 0 || size.Height <= 0)
                return Rectangle.Empty;
            if (size.Width < tmp.Size.Width || size.Height < tmp.Size.Height)
                return Rectangle.Empty;
            return new Rectangle(startPos with { Y = 0 }, size);
        }
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
        return LastNotProcessedIndex(Set);
    }

    public void Process(Mat frame, int frameIndex)
    {
        var index = LastNotProcessedIndex();
        var matchResult = MarkerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
        _status = matchResult.Status;

        switch (matchResult.Status)
        {
            case MatchStatus.Dropped:
                Set[index].Finished = true;
                if (!Finished) Process(frame, frameIndex);
                break;
            case MatchStatus.NotMatched:
                break;
            case MatchStatus.Matched:
            default:
                Set[index].Add(frameIndex, matchResult.Point);
                break;
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
}
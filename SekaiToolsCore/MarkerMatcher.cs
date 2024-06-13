using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Process;
using SekaiStory = SekaiToolsCore.Story.Story;


namespace SekaiToolsCore;

public class MarkerMatcher(VideoInfo videoInfo, SekaiStory storyData, TemplateManager templateManager)
{
    private readonly Dictionary<string, GaMat> _templates = new();

    private GaMat GetTemplate(string content)
    {
        if (_templates.TryGetValue(content, out var template))
            return template;

        var mat = templateManager.GetDbTemplate(content);
        const double resizeRatio = 0.90;
        CvInvoke.Resize(mat, mat, new Size((int)(mat.Width * resizeRatio), (int)(mat.Height * resizeRatio)));
        _templates.Add(content, new GaMat(mat));
        return _templates[content];
    }

    private enum MatchStatus
    {
        NotMatched,
        Matched,
        Dropped
    }

    private MatchStatus _status;

    private struct MatchResult(Point point, MatchStatus status)
    {
        public readonly Point Point = point;
        public readonly MatchStatus Status = status;
    }

    private MatchResult MarkerMatch(Mat img, string text)
    {
        var sText = text[^1].ToString();
        var template = GetTemplate(sText);
        var match = LocalMatch(img, template, TemplateMatchingType.CcoeffNormed);

        switch (_status)
        {
            case MatchStatus.Matched:
                return new MatchResult(match, match.IsEmpty ? MatchStatus.Dropped : MatchStatus.Matched);
            case MatchStatus.NotMatched:
            case MatchStatus.Dropped:
                return new MatchResult(match, match.IsEmpty ? MatchStatus.NotMatched : MatchStatus.Matched);
            default:
                throw new ArgumentOutOfRangeException();
        }


        Point LocalMatch(Mat src, GaMat tmp, TemplateMatchingType matchingType)
        {
            var cropArea = new Rectangle(Point.Empty, new Size(
                (int)(tmp.Size.Height * text.Length * 1.5), tmp.Size.Height * 3));
            var imgCropped = new Mat(src, cropArea);
            var result = Matcher.MatchTemplate(imgCropped, tmp, matchingType);

            return result.MaxVal is > 0.75 and < 1 ? result.MaxLoc : Point.Empty;
        }
    }

    public readonly List<MarkerFrameSet> Set = storyData.Markers().Select(d => new MarkerFrameSet(d, videoInfo.Fps))
        .ToList();

    private static int LastNotProcessedIndex(IReadOnlyList<MarkerFrameSet> set)
    {
        for (var i = 0; i < set.Count; i++)
            if (!set[i].Finished)
                return i;
        return -1;
    }

    public int LastNotProcessedIndex() => LastNotProcessedIndex(Set);

    public void Process(Mat frame, int frameIndex)
    {
        var index = LastNotProcessedIndex();
        var matchResult = MarkerMatch(frame, Set[index].Data.BodyOriginal);
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

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;
}
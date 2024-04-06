using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process;
using SekaiStory = SekaiToolsCore.Story.Story;

namespace SekaiToolsCore;

public class BannerMatcher(VideoInfo videoInfo, SekaiStory storyData, TemplateManager templateManager)
{
    private GaMat GetTemplate(string content) => new GaMat(templateManager.GetDbTemplate(content));


    private static string TrimContent(string content)
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

    private MatchStatus BannerMatch(Mat img, string text)
    {
        var sText = TrimContent(text);
        var template = GetTemplate(sText);
        var match = LocalMatch(img, template, TemplateMatchingType.CcoeffNormed);

        switch (_status)
        {
            case MatchStatus.Matched:
                return match ? MatchStatus.Matched : MatchStatus.Dropped;
            case MatchStatus.NotMatched:
            case MatchStatus.Dropped:
                return match ? MatchStatus.Matched : MatchStatus.NotMatched;
            default:
                throw new ArgumentOutOfRangeException();
        }


        bool LocalMatch(Mat src, GaMat tmp, TemplateMatchingType matchingType)
        {
            var cropArea = Utils.FromCenter(
                img.Size.Center(), new Size((int)(tmp.Size.Height * text.Length * 1.5), (int)(tmp.Size.Height * 1.5)));
            var imgCropped = new Mat(src, cropArea);
            var result = Matcher.MatchTemplate(imgCropped, tmp, matchingType);
            return result.MaxVal is > 0.7 and < 1;
        }
    }

    public readonly List<BannerFrameSet> Set = storyData.Banners().Select(d => new BannerFrameSet(d, videoInfo.Fps))
        .ToList();

    private static int LastNotProcessedIndex(IReadOnlyList<BannerFrameSet> set)
    {
        for (var i = 0; i < set.Count; i++)
            if (!set[i].Finished)
                return i;
        return -1;
    }

    public int LastNotProcessedIndex() => LastNotProcessedIndex(Set);

    private enum MatchStatus
    {
        NotMatched,
        Matched,
        Dropped
    }

    private MatchStatus _status;

    public void Process(Mat frame, int frameIndex)
    {
        var index = LastNotProcessedIndex();
        var matchResult = BannerMatch(frame, Set[index].Data.BodyOriginal);
        _status = matchResult;
        switch (matchResult)
        {
            case MatchStatus.Dropped:
                Set[index].Finished = true;
                if (!Finished) Process(frame, frameIndex);
                break;
            case MatchStatus.NotMatched:
                break;
            case MatchStatus.Matched:
            default:
                Set[index].Add(frameIndex);
                break;
        }
    }

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;
}
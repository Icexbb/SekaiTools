using System.Drawing;
using Emgu.CV;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class BannerTemplateMatcher(VideoInfo videoInfo, SekaiStory storyData, TemplateManager templateManager, Config config)
{
    public readonly List<BannerBaseFrameSet> Set = storyData.Banners()
        .Select(d => new BannerBaseFrameSet(d, videoInfo.Fps))
        .ToList();

    private MatchStatus _status;

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;

    private GaMat GetTemplate(string content)
    {
        return new GaMat(templateManager.GetTemplate(TemplateUsage.BannerContent, content));
    }


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

    private MatchStatus BannerMatch(Mat img, string text, int frameIndex = -1)
    {
        var sText = TrimContent(text);
        var template = GetTemplate(sText);
        var match = LocalMatch(img, template);

        return _status switch
        {
            MatchStatus.Matched => match ? MatchStatus.Matched : MatchStatus.Dropped,
            MatchStatus.NotMatched or MatchStatus.Dropped => match ? MatchStatus.Matched : MatchStatus.NotMatched,
            _ => throw new ArgumentOutOfRangeException(nameof(_status), _status, null)
        };

        bool LocalMatch(Mat src, GaMat tmp)
        {
            var cropArea = UtilFunc.FromCenter(img.Size.Center(),
                new Size((int)(tmp.Size.Height * text.Length * 1.5), (int)(tmp.Size.Height * 1.5)));
            var imgCropped = new Mat(src, cropArea);
            MatProcessor.LaplaceSharpen(imgCropped);
            // imgCropped
            var result = TemplateMatcher.Match(imgCropped, tmp, TemplateMatchCachePool.MatchUsage.Banner);

            if (frameIndex != -1)
            {
                Logger.Log(
                    $"{nameof(BannerTemplateMatcher)} Frame {frameIndex} Match Banner {LastNotProcessedIndex()} Result: {result.MaxVal}");
            }

            return !(result.MaxVal < config.MatchingThreshold.BannerNormal) && !(result.MaxVal > 1);
        }
    }

    private static int LastNotProcessedIndex(List<BannerBaseFrameSet> set)
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
        var matchResult = BannerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
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

    private enum MatchStatus
    {
        NotMatched,
        Matched,
        Dropped
    }
}
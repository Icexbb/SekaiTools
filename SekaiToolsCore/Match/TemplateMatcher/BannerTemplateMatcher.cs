using System.Drawing;
using Emgu.CV;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiToolsBase;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;
using SekaiStory = SekaiToolsBase.Story.Story;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class BannerTemplateMatcher(
    VideoInfo videoInfo,
    SekaiStory storyData,
    TemplateManager templateManager,
    Config config)
{
    public readonly List<BannerBaseFrameSet> Set = storyData.Banners()
        .Select(d => new BannerBaseFrameSet(d, videoInfo.Fps))
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
            using var roi = new Mat(src, cropArea);
            var result = TemplateMatcher.Match(roi, tmp, TemplateMatchCachePool.MatchUsage.Banner);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(BannerTemplateMatcher)} Frame {frameIndex} Match Banner {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);

            var threshold = config.MatchingThreshold.BannerNormal;
            var effectiveThreshold = _useFallbackThreshold
                ? Math.Max(threshold * FallbackRatio, AbsMinThreshold)
                : threshold;
            return result.MaxVal > effectiveThreshold && result.MaxVal < 1;
        }
    }

    private int _firstUnfinishedIndex;

    private void AdvanceFirstUnfinished()
    {
        while (_firstUnfinishedIndex < Set.Count && Set[_firstUnfinishedIndex].Finished)
            _firstUnfinishedIndex++;
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

            var matchResult = BannerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
            _status = matchResult;
            switch (matchResult)
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
                    Set[index].Add(frameIndex);
                    _consecutiveFailures = 0;
                    _useFallbackThreshold = false;
                    return;
            }
        }
    }

    private enum MatchStatus
    {
        NotMatched,
        Matched,
        Dropped
    }
}
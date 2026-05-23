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
    Config config
) : MatcherStateMachine<BannerBaseFrameSet>(
    storyData.Banners().Select(d => new BannerBaseFrameSet(d, videoInfo.Fps)).ToList(),
    (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5)
)
{
    private MatchStatus _status;

    public int LastNotProcessedIndex() => NextUnfinishedIndex();

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

            return result.MaxVal > EffectiveThreshold(config.MatchingThreshold.BannerNormal) && result.MaxVal < 1;
        }
    }

    public void Process(Mat frame, int frameIndex)
    {
        while (!Finished)
        {
            var index = NextUnfinishedIndex();
            if (index < 0) return;

            ResetForNewTarget(index);

            var matchResult = BannerMatch(frame, Set[index].Data.BodyOriginal, frameIndex);
            _status = matchResult;
            switch (matchResult)
            {
                case MatchStatus.Dropped:
                    MarkDropped(index);
                    continue;
                case MatchStatus.NotMatched:
                    if (TryEnterFallback()) continue;
                    return;
                case MatchStatus.Matched:
                default:
                    Set[index].Add(frameIndex);
                    MarkSucceeded();
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

    public BannerMatcherStateDto SaveState()
    {
        var (cf, lfi, uft) = SaveFallbackState();
        return new BannerMatcherStateDto
        {
            Status = (int)_status,
            ConsecutiveFailures = cf,
            LastFailedIndex = lfi,
            UseFallbackThreshold = uft,
            FrameSets = Set.Select(b => new BannerFrameSetDto
            {
                Finished = b.Finished,
                Start = b.IsEmpty() ? -1 : b.StartIndex(),
                End = b.IsEmpty() ? -1 : b.EndIndex()
            }).ToList()
        };
    }

    public void RestoreState(BannerMatcherStateDto state)
    {
        _status = (MatchStatus)state.Status;
        RestoreFallbackState(state.ConsecutiveFailures, state.LastFailedIndex, state.UseFallbackThreshold);

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
}

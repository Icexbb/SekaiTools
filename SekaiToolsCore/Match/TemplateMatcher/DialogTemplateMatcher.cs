using System.Drawing;
using Emgu.CV;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiToolsBase;
using SekaiToolsBase.Story;
using SekaiToolsCore.Process.Config;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Utils;

namespace SekaiToolsCore.Match.TemplateMatcher;

public class DialogTemplateMatcher(
    VideoInfo videoInfo,
    Story storyData,
    TemplateManager templateManager,
    Config config
)
{
    public readonly List<DialogBaseFrameSet> Set =
        storyData.Dialogs().Select(d => new DialogBaseFrameSet(d, videoInfo.Fps)).ToList();

    private Point _nameTagPosition;

    private MatchStatus _status = 0;

    private int _consecutiveFailures;
    private int _lastFailedIndex = -1;
    private bool _useFallbackThreshold;
    private const double FallbackRatio = 0.7;
    private const double AbsMinThreshold = 0.40;
    private int FallbackTriggerFrames => (int)Math.Ceiling(videoInfo.Fps.Fps() * 0.5);

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;

    private GaMat GetNameTag(string name)
    {
        return new GaMat(templateManager.GetTemplate(TemplateUsage.DialogNameTag, name));
    }

    private Point DialogMatchNameTag(Mat img, DialogBaseFrameSet dialogBase, int frameIndex = -1)
    {
        var content = dialogBase.Data.CharacterOriginal;
        var template = GetNameTag(TrimTemplateContent(content));
        var res = LocalMatch(img, template,
            dialogBase.Data.Shake
                ? config.MatchingThreshold.DialogNametagSpecial
                : config.MatchingThreshold.DialogNametagNormal);
        if (!res.IsEmpty && _nameTagPosition.IsEmpty) _nameTagPosition = res;
        return res;

        Point LocalMatch(Mat src, GaMat tmp, double threshold)
        {
            var roi = LocalGetCropArea(tmp.Size);
            using var imgCropped = new Mat(src, roi);
            var result = TemplateMatcher.Match(imgCropped, tmp, TemplateMatchCachePool.MatchUsage.DialogNameTag);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Name Tag {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);


            var effectiveThreshold = _useFallbackThreshold
                ? Math.Max(threshold * FallbackRatio, AbsMinThreshold)
                : threshold;
            if (effectiveThreshold < result.MaxVal && result.MaxVal < 1)
                return new Point(result.MaxLoc.X + roi.X, result.MaxLoc.Y + roi.Y);

            return Point.Empty;
        }

        Rectangle LocalGetCropArea(Size ntt)
        {
            var dialogAreaSize = GetDialogAreaSize();
            var rect = new Rectangle
            {
                X = (videoInfo.Resolution.Width - dialogAreaSize.Width) / 2,
                Y = (videoInfo.Resolution.Height - dialogAreaSize.Height - (int)(ntt.Height * 1.1)) / 1,
                Height = (int)(ntt.Height * 1.8),
                Width = (int)(ntt.Width + ntt.Height * 1.8)
            };
            if (dialogBase.Data.Shake)
                rect.Extend(0.6);

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

        static string TrimTemplateContent(string origin, int maxLen = 3)
        {
            var trimmed = "";
            var len = 0D;
            foreach (var c in origin)
            {
                trimmed += c;
                len += char.IsAscii(c) ? 0.5 : 1;
                if (len >= maxLen) break;
            }

            if (trimmed.Contains('・'))
                trimmed = trimmed[..trimmed.IndexOf('・')];

            return trimmed;
        }
    }

    private MatchStatus DialogMatchContent(Mat img, DialogBaseFrameSet dialogBase, Point point,
        MatchStatus lastStatus = 0,
        int frameIndex = -1)
    {
        var content = dialogBase.Data.BodyOriginal;
        if (point.X == 0) return 0;
        var charTemplates = GetDialogInd();
        var template1 = charTemplates[0];
        var template2 = charTemplates[1];
        var template3 = charTemplates[2];

        bool matchRes;

        var matchingThreshold = dialogBase.Data.Shake
            ? config.MatchingThreshold.DialogContentSpecial
            : config.MatchingThreshold.DialogContentNormal;

        switch (lastStatus)
        {
            case MatchStatus.DialogNotMatched:
            {
                matchRes = LocalMatch(img, template1, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent1);
                return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogNotMatched;
            }
            case MatchStatus.DialogMatched1:
            {
                matchRes = LocalMatch(img, template2, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent2);
                if (matchRes) return MatchStatus.DialogMatched2;
                matchRes = LocalMatch(img, template1, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent1);
                return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogDropped;
            }
            case MatchStatus.DialogMatched2:
            {
                matchRes = LocalMatch(img, template3, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent3);
                if (matchRes) return MatchStatus.DialogMatched3;
                matchRes = LocalMatch(img, template2, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent2);
                return matchRes ? MatchStatus.DialogMatched2 : MatchStatus.DialogDropped;
            }
            case MatchStatus.DialogMatched3:
            {
                matchRes = LocalMatch(img, template3, matchingThreshold,
                    TemplateMatchCachePool.MatchUsage.DialogContent3);
                return matchRes ? MatchStatus.DialogMatched3 : MatchStatus.DialogDropped;
            }
            case MatchStatus.NameTagNotMatched:
            case MatchStatus.DialogDropped:
            default:
                return MatchStatus.DialogNotMatched;
        }


        bool LocalMatch(Mat src, GaMat tmp, double threshold, TemplateMatchCachePool.MatchUsage usage)
        {
            var offset = TemplateManager.GetFontSize(src.Size);
            Rectangle dialogStartPosition = new(
                point.X + (int)(0.1 * offset),
                point.Y + (int)(1.1 * offset),
                (int)(4.0 * offset),
                (int)(2.0 * offset)
            );
            if (dialogBase.Data.Shake)
                dialogStartPosition.Extend(0.6);
            dialogStartPosition.Limit(new Rectangle(Point.Empty, videoInfo.Resolution));

            using var imgCropped = new Mat(src, dialogStartPosition);
            var result = TemplateMatcher.Match(imgCropped, tmp, usage);

            if (frameIndex != -1)
                Logger.Log(
                    $"{nameof(DialogTemplateMatcher)} Frame {frameIndex} Match Dialog Content {LastNotProcessedIndex()} Result: {result.MaxVal}",
                    ExtLogLevel.Debug);

            var effectiveThreshold = _useFallbackThreshold
                ? Math.Max(threshold * FallbackRatio, AbsMinThreshold)
                : threshold;
            return result.MaxVal > effectiveThreshold && result.MaxVal < 1;
        }

        List<GaMat> GetDialogInd()
        {
            var dialogBody1 = content[..1];
            var dialogBody2 = content[..2];
            var dialogBody3 = content.Length >= 3 ? content[..3] : content[..2];
            var mat1 = templateManager.GetTemplate(TemplateUsage.DialogContent, dialogBody1);
            var mat2 = templateManager.GetTemplate(TemplateUsage.DialogContent, dialogBody2);
            var mat3 = templateManager.GetTemplate(TemplateUsage.DialogContent, dialogBody3);
            return [new GaMat(mat1), new GaMat(mat2), new GaMat(mat3)];
        }
    }

    private int _firstUnfinishedIndex;

    private void AdvanceFirstUnfinished()
    {
        while (_firstUnfinishedIndex < Set.Count && Set[_firstUnfinishedIndex].Finished)
            _firstUnfinishedIndex++;
    }

    private static int LastNotProcessedIndex(IReadOnlyList<DialogBaseFrameSet> set)
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

    public bool Process(Mat frame, int frameIndex)
    {
        MatchStatus? firstStatus = null;

        while (!Finished)
        {
            var dIndex = LastNotProcessedIndex(Set);
            if (dIndex < 0) break;

            if (dIndex != _lastFailedIndex)
            {
                _lastFailedIndex = dIndex;
                _consecutiveFailures = 0;
                _useFallbackThreshold = false;
            }

            var dialogRefers = Set[dIndex];
            var matchResult = MatchForDialog(frame, dialogRefers, frameIndex);
            _status = matchResult.Status;
            firstStatus ??= matchResult.Status;

            switch (_status)
            {
                case MatchStatus.DialogDropped:
                    Set[dIndex].Finished = true;
                    _consecutiveFailures = 0;
                    _useFallbackThreshold = false;
                    TemplateMatchCachePool.NextDialog();
                    continue;
                case MatchStatus.DialogNotMatched or MatchStatus.NameTagNotMatched:
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= FallbackTriggerFrames && !_useFallbackThreshold)
                    {
                        _useFallbackThreshold = true;
                        _consecutiveFailures = 0;
                        continue;
                    }
                    _useFallbackThreshold = false;
                    return IsStatusMatched(firstStatus.Value);
                default:
                    Set[dIndex].Add(frameIndex, matchResult.Point);
                    _consecutiveFailures = 0;
                    _useFallbackThreshold = false;
                    return IsStatusMatched(firstStatus.Value);
            }
        }

        return IsStatusMatched(firstStatus ?? MatchStatus.DialogNotMatched);
    }

    private static bool IsStatusMatched(MatchStatus status)
    {
        return status is MatchStatus.DialogMatched1
            or MatchStatus.DialogMatched2
            or MatchStatus.DialogMatched3;
    }

    private MatchResult MatchForDialog(Mat frame, DialogBaseFrameSet dialogBase, int frameIndex)
    {
        var lastStatus = _status;
        if (lastStatus is MatchStatus.DialogDropped && dialogBase.IsEmpty())
            lastStatus = MatchStatus.DialogNotMatched;
        Point point;
        if (dialogBase.Data.Shake || dialogBase.IsEmpty())
            point = DialogMatchNameTag(frame, dialogBase, frameIndex);
        else
            point = dialogBase.Start().Point;

        if (point.IsEmpty)
            return new MatchResult(Point.Empty, IsStatusMatched(lastStatus)
                ? MatchStatus.DialogDropped
                : MatchStatus.NameTagNotMatched);

        return new MatchResult(point, DialogMatchContent(frame, dialogBase, point, lastStatus, frameIndex));
    }

    private enum MatchStatus
    {
        NameTagNotMatched = -2,
        DialogNotMatched = 0,
        DialogMatched1 = 1,
        DialogMatched2 = 2,
        DialogMatched3 = 3,
        DialogDropped = -1
    }


    private struct MatchResult(Point point, MatchStatus status)
    {
        public readonly Point Point = point;
        public readonly MatchStatus Status = status;
    }
}
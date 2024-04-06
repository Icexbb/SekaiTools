using System.Drawing;
using Emgu.CV;
using SekaiToolsCore.Process;

namespace SekaiToolsCore;

public class DialogMatcher(VideoInfo videoInfo, Story.Story storyData, TemplateManager templateManager)
{
    private Point _nameTagPosition;

    private GaMat GetNameTag(string name) => new(templateManager.GetEbTemplate(name));

    private Point DialogMatchNameTag(Mat img, string content)
    {
        var template = GetNameTag(TrimTemplateContent(content));
        var res = LocalMatch(img, template, 0.7);
        if (!res.IsEmpty && _nameTagPosition.IsEmpty) _nameTagPosition = res;
        return res;

        Point LocalMatch(Mat src, GaMat tmp, double threshold)
        {
            var roi = LocalGetCropArea(tmp.Size);
            var imgCropped = new Mat(src, roi);
            var result = Matcher.MatchTemplate(imgCropped, tmp);
            if (!(threshold < result.MaxVal) || !(result.MaxVal < 1)) return Point.Empty;
            return new Point(result.MaxLoc.X + roi.X, result.MaxLoc.Y + roi.Y);
        }

        Rectangle LocalGetCropArea(Size ntt)
        {
            var dialogAreaSize = GetDialogAreaSize();
            var rect = new Rectangle
            {
                X = (videoInfo.Resolution.Width - dialogAreaSize.Width) / 2,
                Y = (videoInfo.Resolution.Height - dialogAreaSize.Height - ntt.Height) / 1,
                Height = (int)(ntt.Height * 1.4),
                Width = (int)(ntt.Width + ntt.Height * 0.8),
            };

            if (rect.X < 0) rect.X = 0;
            if (rect.Y < 0) rect.Y = 0;
            if (rect.X + rect.Width > img.Size.Width)
                rect.Width = img.Size.Width - rect.X;
            if (rect.Y + rect.Height > img.Size.Height)
                rect.Height = img.Size.Height - rect.Y;
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

    private MatchStatus DialogMatchContent(Mat img, string content, Point point, MatchStatus lastStatus = 0)
    {
        if (point.X == 0) return 0;
        var charTemplates = GetDialogInd();
        var template1 = charTemplates[0];
        var template2 = charTemplates[1];
        var template3 = charTemplates[2];

        bool matchRes;

        switch (lastStatus)
        {
            case MatchStatus.DialogNotMatched:
            {
                matchRes = LocalMatch(img, template1);
                return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogNotMatched;
            }
            case MatchStatus.DialogMatched1:
            {
                matchRes = LocalMatch(img, template2);
                if (matchRes) return MatchStatus.DialogMatched2;
                matchRes = LocalMatch(img, template1);
                return matchRes ? MatchStatus.DialogMatched1 : MatchStatus.DialogDropped;
            }
            case MatchStatus.DialogMatched2:
            {
                matchRes = LocalMatch(img, template3);
                if (matchRes) return MatchStatus.DialogMatched3;
                matchRes = LocalMatch(img, template2);
                return matchRes ? MatchStatus.DialogMatched2 : MatchStatus.DialogDropped;
            }
            case MatchStatus.DialogMatched3:
            {
                matchRes = LocalMatch(img, template3);
                return matchRes ? MatchStatus.DialogMatched3 : MatchStatus.DialogDropped;
            }
            case MatchStatus.NameTagNotMatched:
            case MatchStatus.DialogDropped:
            default:
                return MatchStatus.DialogNotMatched;
        }


        bool LocalMatch(Mat src, GaMat tmp, double threshold = 0.8)
        {
            var offset = templateManager.DbTemplateMaxSize().Height;
            Rectangle dialogStartPosition = new(
                x: point.X + (int)(0.15 * offset),
                y: point.Y + (int)(1.2 * offset),
                width: (int)(3.5 * offset),
                height: (int)(1.5 * offset)
            );
            var imgCropped = new Mat(src, dialogStartPosition);
            var result = Matcher.MatchTemplate(imgCropped, tmp);
            return result.MaxVal > threshold && result.MaxVal < 1;
        }

        List<GaMat> GetDialogInd()
        {
            var dialogBody1 = content[..1];
            var dialogBody2 = content[..2];
            var dialogBody3 = content.Length >= 3 ? content[..3] : content[..2];
            var mat1 = templateManager.GetDbTemplate(dialogBody1);
            var mat2 = templateManager.GetDbTemplate(dialogBody2);
            var mat3 = templateManager.GetDbTemplate(dialogBody3);
            return [new GaMat(mat1), new GaMat(mat2), new GaMat(mat3)];
        }
    }

    public readonly List<DialogFrameSet> Set =
        storyData.Dialogs().Select(d => new DialogFrameSet(d, videoInfo.Fps)).ToList();

    private static int LastNotProcessedIndex(IReadOnlyList<DialogFrameSet> set)
    {
        for (var i = 0; i < set.Count; i++)
            if (!set[i].Finished)
                return i;
        return -1;
    }

    public int LastNotProcessedIndex() => LastNotProcessedIndex(Set);

    private MatchStatus _status = 0;

    public bool Finished => Set.All(d => d.Finished) || Set.Count == 0;

    public bool Process(Mat frame, int frameIndex)
    {
        var dIndex = LastNotProcessedIndex(Set);
        var dialogRefers = Set[dIndex];

        var matchResult = MatchForDialog(frame, dialogRefers, _status);

        _status = matchResult.Status;
        switch (matchResult.Status)
        {
            case MatchStatus.DialogDropped:
                Set[dIndex].Finished = true;
                if (!Finished) Process(frame, frameIndex);
                break;
            case MatchStatus.DialogNotMatched or MatchStatus.NameTagNotMatched:
                break;
            default:
                Set[dIndex].Add(frameIndex, matchResult.Point);
                break;
        }

        return IsStatusMatched(matchResult.Status);
    }

    private enum MatchStatus
    {
        NameTagNotMatched = -2,
        DialogNotMatched = 0,
        DialogMatched1 = 1,
        DialogMatched2 = 2,
        DialogMatched3 = 3,
        DialogDropped = -1,
    }

    private static bool IsStatusMatched(MatchStatus status) => status is MatchStatus.DialogMatched1
        or MatchStatus.DialogMatched2
        or MatchStatus.DialogMatched3;


    private struct MatchResult(Point point, MatchStatus status)
    {
        public readonly Point Point = point;
        public readonly MatchStatus Status = status;
    }

    private MatchResult MatchForDialog(Mat frame, DialogFrameSet dialog, MatchStatus status)
    {
        Point point;
        if (dialog.Data.Shake || dialog.IsEmpty)
            point = DialogMatchNameTag(frame, dialog.Data.CharacterOriginal);
        else
            point = dialog.Start().Point;

        if (point.IsEmpty)
            return new MatchResult(Point.Empty, IsStatusMatched(status)
                ? MatchStatus.DialogDropped
                : MatchStatus.NameTagNotMatched);

        return new MatchResult(point, DialogMatchContent(frame, dialog.Data.BodyOriginal, point, status));
    }
}
using System.Drawing;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SekaiToolsCore.Process;
using SekaiToolsCore.Story.Event;
using SekaiToolsCore.SubStationAlpha;
using SekaiToolsCore.SubStationAlpha.AssDraw;
using SekaiToolsCore.SubStationAlpha.Tag;
using SekaiToolsCore.SubStationAlpha.Tag.Modded;
using SubtitleEvent = SekaiToolsCore.SubStationAlpha.Event;

namespace SekaiToolsCore;

public class VideoProcess
{
    private readonly long _createTime;

    private readonly Story.Story _storyData;

    private readonly VideoInfo _videoInfo;

    private Point _nameTagPosition = Point.Empty;

    private readonly TemplateManager _templateManager;

    private readonly IProgress<TaskLog>? _progressCarrier;
    private double _progressedFrameCount;
    private double _processStep;

    private readonly Config _config;

    public VideoProcess(Config config, IProgress<TaskLog>? progress = null)
    {
        _progressCarrier = progress;
        _config = config;

        _storyData = Story.Story.FromFile(_config.ScriptFilePath, _config.TranslateFilePath);
        _createTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        _videoInfo = new VideoInfo(_config.VideoFilePath);


        var names = _storyData.Dialogs().Select(dialog => dialog.CharacterOriginal).ToList();
        var dbs = new List<string>();
        foreach (var dialog in _storyData.Dialogs())
        {
            dbs.Add(dialog.BodyOriginal[..1]);
            dbs.Add(dialog.BodyOriginal[..2]);
            if (dialog.BodyOriginal.Length >= 3) dbs.Add(dialog.BodyOriginal[..3]);
        }

        _templateManager = new TemplateManager(_videoInfo.Resolution, dbs, names);
    }

    private MatchResult MatchTemplate(Mat img, GaMat tmp,
        TemplateMatchingType matchingType = TemplateMatchingType.CcoeffNormed)
    {
        var matchResult = new Mat();
        CvInvoke.MatchTemplate(img, tmp.Gray, matchResult, matchingType, mask: tmp.Alpha);
        matchResult.MatRemoveErrorInf();
        double maxVal = 0, minVal = 0;
        Point minLoc = new(), maxLoc = new();

        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
        matchResult.Dispose();

        if (false && _progressCarrier == null)
        {
            var show = img.Clone()!;
            var temp = tmp.Gray.Clone();
            var emptyMat = new Mat(show.Rows - temp.Rows, temp.Cols, temp.Depth, temp.NumberOfChannels);
            emptyMat.SetTo(new MCvScalar(0));
            CvInvoke.VConcat(new VectorOfMat(emptyMat, temp), temp);
            CvInvoke.HConcat(new VectorOfMat(show, temp), show);
            temp.Dispose();

            CvInvoke.PutText(show, $"MaxVal: {maxVal:0.00}", maxLoc with { Y = maxLoc.Y - 5 },
                FontFace.HersheySimplex, 0.4, new MCvScalar(255));
            CvInvoke.Rectangle(show, new Rectangle(maxLoc, tmp.Size), new MCvScalar(255), 2);

            ShowImg(show, $"MaxVal: {maxVal:0.00}");
        }

        return new MatchResult(maxVal, minVal, maxLoc, minLoc);
    }

    private void ShowImg(Mat img, string title = "Image")
    {
        CvInvoke.Imshow("Image", img);
        CvInvoke.SetWindowTitle("Image", title);
        CvInvoke.WaitKey(1);
    }

    #region Log

    private void Report(TaskLog info)
    {
        if (_progressCarrier == null)
        {
            switch (info)
            {
                case TaskLogProgress:
                    break;
                case TaskLogContext context:
                    Console.WriteLine($"Context: {context.Content}");
                    break;
                case TaskLogRequest request:
                {
                    foreach (var item in request.Collection)
                        Console.WriteLine($"Request: {item.StartFrame} -> {item.EndFrame} : {item.Content}");
                    break;
                }
                case TaskLogException exception:
                    Console.WriteLine($"Exception: {exception.Exception.StackTrace}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(info), info, null);
            }
        }
        else _progressCarrier.Report(info);
    }

    private void Log(string content = "")
    {
        Report(new TaskLogContext(content));
    }

    private void Log(double? add = null)
    {
        _progressedFrameCount += add ?? _processStep;
        Report(new TaskLogProgress(_progressedFrameCount, _videoInfo.FrameCount, _createTime));
    }

    private void Log(Exception e)
    {
        Report(new TaskLogException(e));
    }

    private void ReportFinished()
    {
        _progressedFrameCount = _videoInfo.FrameCount;
        Log(0);
    }

    #endregion

    #region Start

    private int MatchContentStarted()
    {
        var videoCap = new VideoCapture(_config.VideoFilePath);
        var videoFrame = new Mat();
        var menuSign = new GaMat(_templateManager.GetMenuSign(), false);
        const double startThreshold = 0.9;

        while (videoCap.Read(videoFrame))
        {
            if (_setStop) return 0;
            CvInvoke.CvtColor(videoFrame, videoFrame, ColorConversion.Bgr2Gray);
            var roi = new Rectangle(
                videoFrame.Width - menuSign.Size.Width * 2, 0,
                menuSign.Size.Width * 2, menuSign.Size.Height * 2
            );
            roi.Extend(0.1);
            var frameCropped = new Mat(videoFrame, roi);
            var result = MatchTemplate(frameCropped, menuSign);

            Log(1);

            if (!(result.MaxVal > startThreshold)) continue;
            return (int)videoCap.Get(CapProp.PosFrames);
        }

        return 0;
    }

    #endregion

    #region Dialog

    #region Match

    private GaMat GetNameTag(string name)
    {
        return new GaMat(_templateManager.GetEbTemplate(name));
    }

    private Rectangle DialogMatchNameTag(Mat img, int dialogIndex)
    {
        var template = GetNameTag(ShortName(_storyData.Dialogs()[dialogIndex].CharacterOriginal));

        var res = LocalMatch(img, template, 0.7, TemplateMatchingType.CcoeffNormed);

        if (!res.IsEmpty && _nameTagPosition.IsEmpty) _nameTagPosition = new Point(res.X, res.Y);
        return res;

        Rectangle LocalMatch(Mat src, GaMat tmp, double threshold, TemplateMatchingType matchingType)
        {
            var cropArea = LocalGetCropArea(tmp.Size);
            var imgCropped = new Mat(src, cropArea);
            var result = MatchTemplate(imgCropped, tmp);
            if (!(threshold < result.MaxVal) || !(result.MaxVal < 1)) return Rectangle.Empty;
            var resPoint = new Point(result.MaxLoc.X + cropArea.X, result.MaxLoc.Y + cropArea.Y);
            return new Rectangle(resPoint, tmp.Size);
        }

        Rectangle LocalGetCropArea(Size ntt)
        {
            var dialogAreaSize = GetDialogAreaSize();
            var rect = new Rectangle
            {
                X = (_videoInfo.Resolution.Width - dialogAreaSize.Width) / 2,
                Y = (_videoInfo.Resolution.Height - dialogAreaSize.Height - ntt.Height) / 1,
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
            return _videoInfo.FrameRatio > 16.0 / 9
                ? new Size
                {
                    Height = (int)(0.237 * _videoInfo.Resolution.Height),
                    Width = (int)(1.389 * _videoInfo.Resolution.Height)
                }
                : new Size
                {
                    Height = (int)(0.133 * _videoInfo.Resolution.Width),
                    Width = (int)(0.781 * _videoInfo.Resolution.Width)
                };
        }

        string ShortName(string oName)
        {
            var shortName = "";
            var len = 0D;
            foreach (var c in oName)
            {
                shortName += c;
                len += char.IsAscii(c) ? 0.5 : 1;
                if (len >= 3) break;
            }

            if (shortName.Contains('・'))
                shortName = shortName[..shortName.IndexOf('・')];

            return shortName;
        }
    }

    private int DialogMatchContent(Mat img, int index, Rectangle nameTagRect, int lastStatus = 0)
    {
        if (nameTagRect.X == 0) return 0;
        var charTemplates = GetDialogInd(index);
        var template1 = charTemplates[0];
        var template2 = charTemplates[1];
        var template3 = charTemplates[2];

        bool matchRes;

        switch (lastStatus)
        {
            case 0:
            {
                matchRes = LocalMatch(img, template1);
                return matchRes ? 1 : 0;
            }
            case 1:
            {
                matchRes = LocalMatch(img, template2);
                if (matchRes) return 2;
                matchRes = LocalMatch(img, template1);
                return matchRes ? 1 : -1;
            }
            case 2:
            {
                matchRes = LocalMatch(img, template3);
                if (matchRes) return 3;

                matchRes = LocalMatch(img, template2);
                return matchRes ? 2 : -1;
            }
            case 3:
            {
                matchRes = LocalMatch(img, template3);
                return matchRes ? 3 : -1;
            }
            default:
                return 0;
        }


        bool LocalMatch(Mat src, GaMat tmp, double threshold = 0.8)
        {
            var offset = _templateManager.DbTemplateMaxSize().Height;
            Rectangle dialogStartPosition = new(
                x: nameTagRect.X + (int)(0.15 * offset),
                y: nameTagRect.Y + (int)(1.2 * offset),
                width: (int)(3.5 * offset),
                height: (int)(1.5 * offset)
            );
            var imgCropped = new Mat(src, dialogStartPosition);
            var result = MatchTemplate(imgCropped, tmp);
            return result.MaxVal > threshold && result.MaxVal < 1;
        }

        List<GaMat> GetDialogInd(int dialogIndex)
        {
            if (_storyData.Dialogs().Length <= dialogIndex) throw new IndexOutOfRangeException();
            var dialog = _storyData.Dialogs()[dialogIndex];
            var dialogBody1 = dialog.BodyOriginal[..1];
            var dialogBody2 = dialog.BodyOriginal[..2];
            var dialogBody3 = dialog.BodyOriginal.Length >= 3 ? dialog.BodyOriginal[..3] : dialog.BodyOriginal[..2];

            var mat1 = _templateManager.GetDbTemplate(dialogBody1);
            var mat2 = _templateManager.GetDbTemplate(dialogBody2);
            var mat3 = _templateManager.GetDbTemplate(dialogBody3);

            return [new GaMat(mat1), new GaMat(mat2), new GaMat(mat3)];
        }
    }

    private List<DialogFrameSet> MatchDialogs(int startPosition = 0)
    {
        var videoCap = new VideoCapture(_config.VideoFilePath);
        var videoFrame = new Mat();
        var dialogFrameSets = new List<DialogFrameSet>();
        videoCap.Set(CapProp.PosFrames, startPosition);

        var dialogStatus = 0;
        var dialogRect = Rectangle.Empty;
        var needNextFrame = true;
        var needNextDialog = true;

        while (true)
        {
            if (_setStop) return [];

            if (needNextFrame)
            {
                var readResult = videoCap.Read(videoFrame);
                if (!readResult) break;
                CvInvoke.CvtColor(videoFrame, videoFrame, ColorConversion.Bgr2Gray);
                Log(_processStep);
                needNextFrame = false;
            }

            var frameIndex = FramePos(videoCap);
            if (needNextDialog)
            {
                if (dialogFrameSets.Count == _storyData.Dialogs().Length) break;
                if (dialogFrameSets.Count > 0)
                    Log($"Frame {frameIndex}: Matched Dialogs {dialogFrameSets.Count}/{_storyData.Dialogs().Length}");

                dialogFrameSets.Add(new DialogFrameSet(_storyData.Dialogs()[dialogFrameSets.Count], _videoInfo.Fps));
                dialogRect = Rectangle.Empty;
                dialogStatus = 0;
                needNextFrame = false;
                needNextDialog = false;
            }


            Rectangle nameTagRect;
            if (dialogRect.IsEmpty || dialogFrameSets[^1].DialogData.Shake)
                nameTagRect = DialogMatchNameTag(videoFrame, dialogFrameSets[^1].DialogData.Index);
            else
                nameTagRect = dialogRect;

            if (nameTagRect.IsEmpty)
            {
                if (dialogFrameSets[^1].IsEmpty) needNextFrame = true;
                else needNextDialog = true;
            }
            else
            {
                var currentDialogStatus = DialogMatchContent(
                    videoFrame, dialogFrameSets[^1].DialogData.Index, nameTagRect, dialogStatus);
                needNextFrame = true;
                switch (currentDialogStatus)
                {
                    case 3 or 2 or 1:
                    {
                        dialogFrameSets[^1].Add(frameIndex, nameTagRect);
                        break;
                    }
                    case -1:
                    {
                        needNextDialog = true;
                        needNextFrame = false;
                        break;
                    }
                }

                dialogStatus = currentDialogStatus;
            }
        }

        Log(_processStep * (_videoInfo.FrameCount - FramePos(videoCap)));
        videoFrame.Dispose();
        return dialogFrameSets;

        int FramePos(VideoCapture vc) => (int)vc.Get(CapProp.PosFrames);
    }

    #endregion

    #region Events

    private static Queue<char> FormatDialogBodyArr(string body)
    {
        var bodyCopy = body.Replace("\\N", "\n").Replace("\\n", "\n");
        var lineCount = bodyCopy.Count(t => t == '\n');
        if (lineCount == 2) bodyCopy = bodyCopy.Replace("\n", "");
        var queue = new Queue<char>();
        foreach (var c in bodyCopy) queue.Enqueue(c);
        return queue;
    }

    private string MakeDialogTypewriter(string body)
    {
        var queue = FormatDialogBodyArr(body);
        var fadeTime = _config.TyperSetting.FadeTime;
        var charTime = _config.TyperSetting.CharTime;
        if (fadeTime <= 0 && charTime <= 0)
            return string.Join("", queue);

        var sb = new StringBuilder();
        sb.Append(queue.Dequeue());

        var nextStart = 0;
        foreach (var s in queue)
        {
            var ft = fadeTime / (char.IsAscii(s) ? 2 : 1);
            var ct = charTime / (char.IsAscii(s) ? 2 : 1);

            var start = nextStart + (s == '\n' ? 300 : 0);
            var alphaTag = $@"{{\alphaFF\t({start},{start + ft},1,\alpha0)}}";
            sb.Append(alphaTag);
            sb.Append(s == '\n' ? "\\N" : s);
            nextStart = start + ct;
        }

        return sb.ToString();
    }

    private string MakeDialogTypewriter(string body, int frameCount)
    {
        var queue = FormatDialogBodyArr(body);
        var fadeTime = _config.TyperSetting.FadeTime;
        var charTime = _config.TyperSetting.CharTime;
        if (fadeTime <= 0 && charTime <= 0)
            return string.Join("", queue);

        var nowTime = (int)(1000 / _videoInfo.Fps.Fps() * frameCount);
        var charTimeEnd = 0;
        var sb = new StringBuilder();
        sb.Append(queue.Dequeue());
        while (queue.Count != 0)
        {
            var s = queue.Dequeue();
            var ft = fadeTime / (char.IsAscii(s) ? 2 : 1);
            var ct = charTime / (char.IsAscii(s) ? 2 : 1);

            charTimeEnd += ct;
            charTimeEnd += s == '\n' ? 300 : 0;

            int alphaPercent;
            if (nowTime <= charTimeEnd - ft)
                alphaPercent = 100;
            else if (nowTime < charTimeEnd)
                alphaPercent = (charTimeEnd - nowTime) * 100 / ft;
            else
                alphaPercent = 0;

            var alphaTag = $@"{{\alpha{Convert.ToString((int)(255 * alphaPercent / 100.0), 16).ToUpper()}}}";
            if (alphaPercent != 0) sb.Append(alphaTag);
            sb.Append(s == '\n' ? "\\N" : s);
            if (alphaPercent == 100) break;
        }

        foreach (var s in queue) sb.Append(s == '\n' ? "\\N" : s);

        return sb.ToString();
    }

    private struct LineSepInfo(int frame, int substr)
    {
        public readonly int Frame = frame;
        public readonly int Substr = substr;
    }

    private readonly Dictionary<int, LineSepInfo> _separateLineCollection = new();

    public void SetSeparateLine(int key, int separateFramePosition, int substringPosition)
    {
        _separateLineCollection[key] = new LineSepInfo(separateFramePosition, substringPosition);
    }

    private List<Style> MakeDialogStyles()
    {
        var fontsize = (int)((_videoInfo.FrameRatio > 16.0 / 9
            ? _videoInfo.Resolution.Height * 0.043
            : _videoInfo.Resolution.Width * 0.024) * (70 / 61D));

        var outlineSize = (int)Math.Ceiling(fontsize / 15.0);
        var marginV = _nameTagPosition.Y + (int)(fontsize * 2.3);
        var marginH = _nameTagPosition.X + (int)(fontsize * 0.4);

        var charaFontsize = (int)(fontsize * 0.9);
        var charaOutlineSize = (int)Math.Ceiling(charaFontsize / 15.0);
        const string fontName = "思源黑体 CN Bold";

        var blackColor = new AlphaColor(0, 255, 255, 255);
        var outlineColor = new AlphaColor(50, 73, 71, 102);
        var result = new List<Style>
        {
            new("Line1", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH, marginV: marginV),

            new("Line2", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01)),

            new("Line3", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01 * 2)),

            new("Character", fontName, charaFontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: charaOutlineSize, shadow: 0, alignment: 7),

            new("Screen", fontName, charaFontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7)
        };

        return result;
    }

    private List<SubtitleEvent> MakeDialogEvents(List<DialogFrameSet> dialogList)
    {
        var result = new List<SubtitleEvent>();
        var needSepCount = new List<RequestItem>();
        foreach (var set in dialogList)
        {
            var needForceReturn =
                set.DialogData.BodyOriginal.Split("\n").Length == 3 && (
                    set.DialogData.FinalContent.Contains("\\R") ||
                    set.DialogData.FinalContent.Replace("\\N", "").Replace("\n", "")
                        .Length > 37
                );
            if (!needForceReturn) continue;
            var item = new RequestItem(set.DialogData.FinalContent, set.StartIndex(), set.EndIndex(),
                _videoInfo.Fps.Fps());
            needSepCount.Add(item);
        }

        Report(new TaskLogRequest(needSepCount));
        if (_progressCarrier != null)
        {
            Log("Waiting for separate line info...");
            while (_separateLineCollection.Count < needSepCount.Count) Thread.Sleep(100);
        }

        var dialogIndex = 0;
        foreach (var set in dialogList)
        {
            dialogIndex++;
            var dialogEvents = new List<SubtitleEvent>();
            var dialogMarker = $"-----  {dialogIndex:000}  -----";
            dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Start",
                set.StartTime(), set.EndTime(), "Screen"));

            if (_progressCarrier != null && _separateLineCollection.TryGetValue(set.StartIndex(), out var info))
            {
                var items = SeparateDialogSet(set, info);
                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Line 1 ↓",
                    set.StartTime(), set.EndTime(), "Screen"));

                dialogEvents.AddRange(GenerateDialogEvent(items[0]));

                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Line 2 ↓",
                    set.StartTime(), set.EndTime(), "Screen"));

                dialogEvents.AddRange(GenerateDialogEvent(items[1]));
            }
            else
            {
                dialogEvents.AddRange(GenerateDialogEvent(set));
            }

            if (dialogEvents.Count > 3)
            {
                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Debug ↓",
                    set.StartTime(), set.EndTime(), "Screen"));
                var t = GenerateNoneJitterDialogEvents(set).Select(item => item.ToComment()).ToList();
                dialogEvents.AddRange(t);
            }

            dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  End",
                set.StartTime(), set.EndTime(), "Screen"));

            result.AddRange(dialogEvents);
        }

        return result;


        List<DialogFrameSet> SeparateDialogSet(DialogFrameSet dialogFrameSet, LineSepInfo info)
        {
            var content = dialogFrameSet.DialogData.FinalContent
                .Replace("\n", "")
                .Replace("\\N", "")
                .Replace("\\R", "");

            var sepCount = info.Frame - dialogFrameSet.StartIndex();

            var sepSet1 = new DialogFrameSet((Dialog)dialogFrameSet.DialogData.Clone(), _videoInfo.Fps);
            var sepSet2 = new DialogFrameSet((Dialog)dialogFrameSet.DialogData.Clone(), _videoInfo.Fps);

            sepSet1.Frames.AddRange(dialogFrameSet.Frames[..sepCount]);
            sepSet2.Frames.AddRange(dialogFrameSet.Frames[sepCount..]);

            sepSet1.DialogData.BodyTranslated = content[..info.Substr];
            sepSet2.DialogData.BodyTranslated = content[info.Substr..];

            return [sepSet1, sepSet2];
        }

        IEnumerable<SubtitleEvent> GenerateDialogEvent(DialogFrameSet set)
        {
            var subtitleEventItems = new List<SubtitleEvent>();
            subtitleEventItems.AddRange(set.IsJitter
                ? GenerateJitterDialogEvents(set)
                : GenerateNoneJitterDialogEvents(set));
            return subtitleEventItems;
        }

        IEnumerable<SubtitleEvent> GenerateNoneJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.DialogData.FinalContent;
            var characterName = dialogFrameSet.DialogData.FinalCharacter;
            var originLineCount = dialogFrameSet.DialogData.BodyOriginal.Split("\n").Length;
            var styleName = "Line" + originLineCount;

            var startTime = dialogFrameSet.StartTime();
            var endTime = dialogFrameSet.EndTime();

            var body = MakeDialogTypewriter(content);

            var dialogItem = SubtitleEvent.Dialog(body, startTime, endTime, styleName);

            var characterItemPosition =
                dialogFrameSet.Start().Point +
                new Size(GetNameTag(dialogFrameSet.DialogData.CharacterOriginal).Size.Width + 10, 0);
            var characterItemPositionTag = $@"{{\pos({characterItemPosition.X},{characterItemPosition.Y})}}";
            var characterItem = SubtitleEvent.Dialog(
                characterItemPositionTag + characterName, startTime, endTime, "Character");
            // if (characterName == "") characterItem = characterItem.ToComment();

            return [characterItem, dialogItem];
        }

        IEnumerable<SubtitleEvent> GenerateJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.DialogData.FinalContent;
            var characterName = dialogFrameSet.DialogData.FinalCharacter;
            var originLineCount = dialogFrameSet.DialogData.BodyOriginal.Split("\n").Length;

            var styleName = "Line" + originLineCount;
            var styles = MakeDialogStyles();
            var style = styles.Find(s => s.Name == styleName)!;

            var constPosition = dialogFrameSet.Start().Point;
            var lastPosition = new Point(0, 0);
            var dialogEvents = new List<SubtitleEvent>();
            var characterEvents = new List<SubtitleEvent>();
            foreach (var frame in dialogFrameSet.Frames)
            {
                var x = style.MarginL;
                var y = style.MarginV;
                x += frame.Point.X - constPosition.X;
                y += frame.Point.Y - constPosition.Y;
                var body = @$"{{\pos({x},{y})}}"
                           + MakeDialogTypewriter(content, frame.Index - dialogFrameSet.StartIndex());

                if (lastPosition.X == x && lastPosition.Y == y && body == dialogEvents[^1].Text)
                {
                    dialogEvents[^1].End = frame.EndTime();
                }
                else
                {
                    var dialogItem = SubtitleEvent.Dialog(body, frame.StartTime(), frame.EndTime(), styleName);
                    dialogEvents.Add(dialogItem);
                }

                if (lastPosition.X == x && lastPosition.Y == y && body == characterEvents[^1].Text)
                {
                    characterEvents[^1].End = frame.EndTime();
                }
                else
                {
                    var offset = GetNameTag(dialogFrameSet.DialogData.CharacterOriginal).Size.Width;
                    var position = frame.Point + new Size(offset + 10, 0);
                    var tag = $@"{{\pos({position.X},{position.Y})}}";

                    var characterItem = SubtitleEvent.Dialog(
                        tag + characterName, frame.StartTime(), frame.EndTime(), "Character");
                    // if (characterName == "") characterItem = characterItem.ToComment();
                    characterEvents.Add(characterItem);
                }

                lastPosition = new Point(x, y);
            }

            var returnVal = new List<SubtitleEvent>();
            returnVal.AddRange(dialogEvents);
            returnVal.AddRange(characterEvents);
            return returnVal;
        }
    }

    #endregion

    #endregion

    #region Banner

    #region Match

    private GaMat GetBannerTemplate(string content)
    {
        return new GaMat(_templateManager.GetDbTemplate(content));
    }

    private static string ShortBannerContent(string content)
    {
        var shortContent = "";
        var len = 0D;
        foreach (var c in content)
        {
            shortContent += c;
            len += char.IsAscii(c) ? 0.5 : 1;
            if (len >= 5) break;
        }

        if (shortContent.Contains('・')) shortContent = shortContent[..shortContent.IndexOf('・')];
        if (shortContent.Contains('　')) shortContent = shortContent[..shortContent.IndexOf('　')];
        if (shortContent.Contains(' ')) shortContent = shortContent[..shortContent.IndexOf(' ')];
        return shortContent;
    }

    private bool BannerMatch(Mat img, string text)
    {
        var sText = ShortBannerContent(text);
        var template = GetBannerTemplate(sText);
        var res = LocalMatch(img, template, TemplateMatchingType.CcoeffNormed);

        return res;

        bool LocalMatch(Mat src, GaMat tmp, TemplateMatchingType matchingType)
        {
            var cropArea = Utils.FromCenter(
                img.Size.Center(), new Size((int)(tmp.Size.Height * text.Length * 1.5), (int)(tmp.Size.Height * 1.5)));
            var imgCropped = new Mat(src, cropArea);
            var result = MatchTemplate(imgCropped, tmp, matchingType);
            return result.MaxVal is > 0.7 and < 1;
        }
    }

    private List<BannerFrameSet> MatchBanners(int startPosition = 0)
    {
        var videoCap = new VideoCapture(_config.VideoFilePath);
        var videoFrame = new Mat();
        videoCap.Set(CapProp.PosFrames, startPosition);

        var bannerFrameSets =
            _storyData.Banners().Select(@event => new BannerFrameSet(@event, _videoInfo.Fps)).ToList();

        var eventIndex = 0;

        var needNextFrame = true;
        var lastRes = false;

        while (true)
        {
            if (_setStop) return [];

            if (needNextFrame)
            {
                var readResult = videoCap.Read(videoFrame);
                if (!readResult) break;
                CvInvoke.CvtColor(videoFrame, videoFrame, ColorConversion.Bgr2Gray);
                Log(_processStep);
            }

            var frameIndex = FramePos(videoCap);


            var res = BannerMatch(videoFrame, bannerFrameSets[eventIndex].Banner.BodyOriginal);
            if (res)
            {
                bannerFrameSets[eventIndex].Add(frameIndex);
                needNextFrame = true;
            } // X 1
            else
            {
                if (lastRes)
                {
                    eventIndex += 1;
                    Log($"Frame {frameIndex}: Matched Banners {eventIndex}/{bannerFrameSets.Count}");
                    if (eventIndex >= bannerFrameSets.Count) break;
                    needNextFrame = false;
                } // 1 0
                else needNextFrame = true; // 0 0
            }

            lastRes = res;
        }

        Log(_processStep * (_videoInfo.FrameCount - FramePos(videoCap)));
        videoFrame.Dispose();
        return bannerFrameSets;

        int FramePos(VideoCapture vc) => (int)vc.Get(CapProp.PosFrames);
    }

    #endregion

    #region Events

    private List<SubtitleEvent> MakeBannerEvents(List<BannerFrameSet> bannerList)
    {
        var result = new List<SubtitleEvent>();
        var count = 0;
        foreach (var set in bannerList)
        {
            count++;

            var events = new List<SubtitleEvent>();
            var dialogMarker = $"-----  {count:000}  -----";
            events.Add(SubtitleEvent.Comment($"{dialogMarker}  Start", set.StartTime(), set.EndTime(), "Screen"));
            events.AddRange(GenerateBannerEvent(set));
            events.Add(SubtitleEvent.Comment($"{dialogMarker}  End", set.StartTime(), set.EndTime(), "Screen"));
            result.AddRange(events);
        }

        return result;

        IEnumerable<SubtitleEvent> GenerateBannerEvent(BannerFrameSet set)
        {
            var offset = _templateManager.DbTemplateMaxSize().Height;
            var center = _videoInfo.Resolution.Center();
            center.Y += (int)(offset * 2.5);
            center.Y = center.Y / 20 * 20;
            var content = set.Banner.FinalContent;
            var startTime = set.StartTime();
            var endTime = set.EndTime();

            var maskFade = set.Banner.Index == 0 ? Tags.Fade(300, 200) : Tags.Fade(100, 200);
            var maskBlur = maskFade + Tags.Blur(30) + Tags.Anchor(7) + Tags.Paint(1);

            var body = maskFade + Tags.Anchor(5) + Tags.FontSize(offset) +
                       Tags.Move(center.X - offset / 3, center.Y, center.X, center.Y, 0, 200) + content;

            var contentItem = SubtitleEvent.Dialog(body, startTime, endTime, "BannerText");

            var cRec = Utils.FromCenter(center,
                new Size((offset * 12) / 20 * 20, (int)(offset * 1.4) / 20 * 20));
            var mRec = Utils.FromCenter(center,
                new Size((offset * 12) / 20 * 20, (int)(offset * 2) / 20 * 20));
            var mask = AssDraw.Rectangle(mRec).ToString();
            var clipLeft = (
                    Tags.Clip(0, cRec.Y, cRec.X, cRec.Y + cRec.Height) +
                    Tags.Transformation(
                        0, 200, Tags.Clip(0, cRec.Y, cRec.X + cRec.Width, cRec.Y + cRec.Height)))
                .ToString();

            var clipRight = (
                    Tags.Clip(cRec.X, cRec.Y, _videoInfo.Resolution.Width, cRec.Y + cRec.Height) +
                    Tags.Transformation(0, 200,
                        Tags.Clip(cRec.X + cRec.Width, cRec.Y,
                            _videoInfo.Resolution.Width, cRec.Y + cRec.Height)))
                .ToString();


            var shift = ModdedTags.LeadingHorizontal(offset * 5) +
                        Tags.Transformation(0, 200, ModdedTags.LeadingHorizontal(0));


            var maskItem1 =
                SubtitleEvent.Dialog(maskBlur + clipLeft + mask, startTime, endTime, "BannerMask");
            var maskItem2 =
                SubtitleEvent.Dialog(maskBlur + clipRight + shift + mask, startTime, endTime, "BannerMask");

            return [maskItem1, maskItem2, contentItem];
        }
    }

    private List<Style> MakeBannerStyles()
    {
        var result = new List<Style>();
        var fontsize = (int)((_videoInfo.FrameRatio > 16.0 / 9
            ? _videoInfo.Resolution.Height * 0.043
            : _videoInfo.Resolution.Width * 0.024) * (70 / 61D));
        const string fontName = "思源黑体 CN Bold";

        var whiteColor = new AlphaColor(0, 255, 255, 255);
        var outlineColor = new AlphaColor(30, 95, 92, 123);
        result.Add(new Style("BannerMask", fontName, fontsize, primaryColour: outlineColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        result.Add(new Style("BannerText", fontName, fontsize, primaryColour: whiteColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        return result;
    }

    #endregion

    #endregion

    #region Contorl

    private bool _setStop;

    public void Stop()
    {
        _setStop = true;
    }

    #endregion


    public string Process()
    {
        var subtitleEventItems = new List<SubtitleEvent>();
        try
        {
            Log("Start To Process");
            var contentStartedAt = MatchContentStarted();
            Log($"Video Content Start At Frame {contentStartedAt}");
            var tasks = new[]
            {
                new Task(() => MatchDialog(contentStartedAt)),
                new Task(() => MatchBanner(contentStartedAt))
            };
            _processStep = 1.0 / tasks.Length;
            foreach (var task in tasks) task.Start();
            Task.WaitAll(tasks);
        }
        catch (Exception e)
        {
            Log(e);
            if (_progressCarrier == null) throw;
        }
        finally
        {
            ReportFinished();
        }

        if (!_setStop)
        {
            Write(subtitleEventItems);
            Log("Process Finished");
        }
        else
        {
            Log("Process Stopped");
        }

        return _setStop ? "" : _config.OutputFilePath;

        void Write(List<SubtitleEvent> eventList)
        {
            var scriptInfo = new ScriptInfo(
                _videoInfo.Resolution.Width, _videoInfo.Resolution.Height,
                Path.GetFileNameWithoutExtension(_config.VideoFilePath));
            var garbage = new Garbage(_config.VideoFilePath, _config.VideoFilePath);
            var style = new Styles(MakeDialogStyles().Concat(MakeBannerStyles()).ToArray());

            var events = new Events(eventList.ToArray());
            var assData = new Subtitle(scriptInfo, garbage, style, events);
            var outputFile = new StreamWriter(_config.OutputFilePath, false, Encoding.UTF8);
            outputFile.Write(assData.ToString());
            outputFile.Close();
        }

        void MatchDialog(int contentStartedAt)
        {
            var matchResult = MatchDialogs(contentStartedAt);
            var events = MakeDialogEvents(matchResult);
            Log(_setStop ? "Dialog Process Aborted" : "Dialog Match Finished");
            subtitleEventItems.Add(SubtitleEvent.Comment("-----  Dialog  -----", Frame.Zero, Frame.Zero, "Screen"));
            subtitleEventItems.AddRange(events);
            subtitleEventItems.Add(SubtitleEvent.Comment("-----  Dialog  -----", Frame.Zero, Frame.Zero, "Screen"));
        }

        void MatchBanner(int contentStartedAt)
        {
            var matchResult = MatchBanners(contentStartedAt);
            var events = MakeBannerEvents(matchResult);
            Log(_setStop ? "Banner Process Aborted" : "Banner Match Finished");
            subtitleEventItems.Add(SubtitleEvent.Comment("-----  Banner  -----", Frame.Zero, Frame.Zero, "Screen"));
            subtitleEventItems.AddRange(events);
            subtitleEventItems.Add(SubtitleEvent.Comment("-----  Banner  -----", Frame.Zero, Frame.Zero, "Screen"));
        }
    }
}
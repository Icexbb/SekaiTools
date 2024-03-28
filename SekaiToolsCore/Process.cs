using System.Drawing;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore;

public class VideoProcessTaskConfig
{
    public string VideoFilePath { get; }
    public string ScriptFilePath { get; }
    public string TranslateFilePath { get; }
    public string OutputFilePath { get; }

    public struct TypewriterSetting(int fadeTime, int charTime)
    {
        public readonly int FadeTime = fadeTime;
        public readonly int CharTime = charTime;
    }

    public TypewriterSetting TyperSetting = new(50, 80);


    public VideoProcessTaskConfig(string videoFilePath, string scriptFilePath,
        string translateFilePath = "", string outputFilePath = "")
    {
        if (!Path.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found.", videoFilePath);
        if (!Path.Exists(scriptFilePath))
            throw new FileNotFoundException("Script file not found.", scriptFilePath);

        VideoFilePath = videoFilePath;
        ScriptFilePath = scriptFilePath;

        if (translateFilePath != "" && !Path.Exists(translateFilePath))
            throw new FileNotFoundException("Translation file not found.", translateFilePath);
        TranslateFilePath = translateFilePath;
        if (outputFilePath != "")
        {
            if (Path.GetExtension(outputFilePath) != ".ass")
                throw new FileNotFoundException("Output Path must be a .ass file ", outputFilePath);
            OutputFilePath = outputFilePath;
        }
        else
        {
            OutputFilePath = Path.Join(Path.GetDirectoryName(videoFilePath),
                "[STGenerated] " + Path.GetFileNameWithoutExtension(videoFilePath) + ".ass");
        }
    }

    public void SetSubtitleTyperSetting(int fadeTime, int charTime)
    {
        TyperSetting = new TypewriterSetting(fadeTime, charTime);
    }

    public override int GetHashCode()
    {
        var contents = $"{VideoFilePath}{ScriptFilePath}{TranslateFilePath}{OutputFilePath}";
        return contents.GetHashCode();
    }
}

public class TemplateGrayAlpha
{
    public readonly Mat Gray;
    public readonly Mat Alpha;
    public Size Size => Gray.Size;

    public TemplateGrayAlpha(IInputArray src, bool resize = true)
    {
        var grayImage = new Mat();
        var alphaChannel = new Mat();
        CvInvoke.CvtColor(src, grayImage, ColorConversion.Bgra2Gray);
        CvInvoke.ExtractChannel(src, alphaChannel, 3);
        if (resize)
        {
            const int scaleRatio = 5;
            var size = new Size(grayImage.Size.Width / scaleRatio, grayImage.Size.Height / scaleRatio);
            CvInvoke.Resize(grayImage, grayImage, size);
            CvInvoke.Resize(alphaChannel, alphaChannel, size);
        }

        Gray = grayImage;
        Alpha = alphaChannel;
    }
}

public class FrameTimeData(int index, FrameRate fps)
{
    public int Index => index;
    public string ExactTime() => fps.TimeAtFrame(index).GetAssFormatted();

    public string StartTime() => fps.TimeAtFrame(index, FrameType.Start).GetAssFormatted();

    public string EndTime() => fps.TimeAtFrame(index, FrameType.End).GetAssFormatted();
}

public class DialogFrameSet(StoryDialogEvent dialogData, FrameRate fps)
{
    public class DialogFrameResult(int index, FrameRate fps, Rectangle nameTagRect)
        : FrameTimeData(index, fps)
    {
        public Point Point => nameTagRect.Location;
    }

    public readonly List<DialogFrameResult> Frames = [];
    public readonly StoryDialogEvent DialogData = dialogData;
    private const int FrameIndexOffset = -1;

    public void Add(int index, Rectangle nameTagRect) =>
        Frames.Add(new DialogFrameResult(index + FrameIndexOffset, fps, nameTagRect));

    public bool IsEmpty => Frames.Count == 0;

    public bool IsJitter => DialogData.Shake;

    public DialogFrameResult Start() => Frames[0];

    public DialogFrameResult End() => Frames[^1];

    public string StartTime() => Start().StartTime();

    public string EndTime() => End().EndTime();

    public int StartIndex() => Start().Index;

    public int EndIndex() => End().Index;
}

// private class BannerFrameResult(int frameIndex) : FrameMatchResult(frameIndex);
//
// private class BannerFrameSet(StoryBannerEvent banner)
// {
//     public readonly List<BannerFrameResult> Data = [];
//     public readonly StoryBannerEvent Banner = banner;
//
//     public bool IsEmpty => Data.Count == 0;
//
//     public string StartTime(double fps) => Data[0].FrameTimeStr(fps);
//     public string EndTime(double fps) => Data[^1].FrameTimeStr(fps);
// }
public enum TaskLogType
{
    Content,
    Progress,
    Request,
    Exception
}

public abstract class TaskLog
{
    public abstract TaskLogType Type { get; }
}

public class TaskLogProgress(double progressedFrameCount, int totalFrameCount, long startProcessTime) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Progress;

    private double ProgressedFrameCount { get; } = progressedFrameCount;
    private int TotalFrameCount { get; } = totalFrameCount;
    private long InfoTimeMilliseconds { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startProcessTime;

    public double Progress => ProgressedFrameCount / TotalFrameCount * 100;
    public double Fps => ProgressedFrameCount / (InfoTimeMilliseconds / 1000.0);

    public bool Finished => Math.Abs(ProgressedFrameCount - TotalFrameCount) < 1;
}

public class TaskLogContext(string content = "") : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Content;
    public string Content { get; } = content;
}

public class RequestItem(string content, int startFrame, int endFrame, double fps)
{
    public string Content { get; } = content;
    public int StartFrame { get; } = startFrame;
    public int EndFrame { get; } = endFrame;
    public double Fps { get; } = fps;
}

public class TaskLogRequest(List<RequestItem> collection) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Request;

    public List<RequestItem> Collection { get; } = collection;
}

public class TaskLogException(Exception exception) : TaskLog
{
    public override TaskLogType Type { get; } = TaskLogType.Exception;
    public Exception Exception { get; } = exception;
}

public class VideoProcess
{
    private readonly long _selfCreateTime;

    private readonly StoryData _storyData;

    private readonly Size _videoResolution;
    private readonly double _videoResolutionRatio;
    private readonly FrameRate _videoFrameRate;
    private readonly int _videoFrameCount;

    private Point _nameTagPosition;

    private readonly List<string> _names;

    private readonly TemplateManager _templateManager;


    private readonly IProgress<TaskLog>? _progressCarrier;
    private double _progressedFrameCount;
    private const double ProcessStep = 1 / 1.0;

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

    private void Log(double add = ProcessStep)
    {
        _progressedFrameCount += add;
        Report(new TaskLogProgress(_progressedFrameCount, _videoFrameCount, _selfCreateTime));
    }

    private void Log(Exception e)
    {
        Report(new TaskLogException(e));
    }

    private void ReportFinished()
    {
        _progressedFrameCount = _videoFrameCount;
        Log(0);
    }

    private readonly VideoProcessTaskConfig _config;

    public VideoProcess(VideoProcessTaskConfig config, IProgress<TaskLog>? progress = null)
    {
        _progressCarrier = progress;
        _config = config;

        _storyData = StoryData.FromFile(_config.ScriptFilePath, _config.TranslateFilePath);
        _selfCreateTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

        var videoCap = new VideoCapture(_config.VideoFilePath);
        var videoFrame = new Mat();
        videoCap.Read(videoFrame);
        _videoResolution = videoFrame.Size;
        _videoResolutionRatio = (double)_videoResolution.Width / _videoResolution.Height;
        _videoFrameRate = new FrameRate(videoCap.Get(CapProp.Fps));
        _videoFrameCount = (int)videoCap.Get(CapProp.FrameCount);
        videoFrame.Dispose();
        videoCap.Dispose();

        _nameTagPosition = new Point(0, 0);
        _names = _storyData.Dialogs().Select(dialog => dialog.CharacterOriginal).ToList();
        var dbTextsChar1 = _storyData.Dialogs().Select(dialog => dialog.BodyOriginal[..1]).ToArray();
        var dbTextsChar2 = _storyData.Dialogs().Select(dialog => dialog.BodyOriginal[..2]).ToArray();
        var dbTextsChar3 = _storyData.Dialogs().Select(dialog =>
            dialog.BodyOriginal.Length >= 3 ? dialog.BodyOriginal[..3] : dialog.BodyOriginal[..2]).ToArray();
        _templateManager = new TemplateManager(
            _videoResolution, dbTextsChar1.Concat(dbTextsChar2).Concat(dbTextsChar3), _names);
    }


    private TemplateGrayAlpha GetNameTag(string name)
    {
        return new TemplateGrayAlpha(_templateManager.GetEbTemplate(name));
    }

    private List<TemplateGrayAlpha> GetDialogInd(int index)
    {
        // if (_dialogIdentifierMats.Count > index) return _dialogIdentifierMats[index].ToList();
        if (_storyData.Dialogs().Length <= index) throw new IndexOutOfRangeException();
        var dialog = _storyData.Dialogs()[index];
        var dialogBody1 = dialog.BodyOriginal[..1];
        var dialogBody2 = dialog.BodyOriginal[..2];
        var dialogBody3 = dialog.BodyOriginal.Length >= 3 ? dialog.BodyOriginal[..3] : dialog.BodyOriginal[..2];

        var mat1 = _templateManager.GetDbTemplate(dialogBody1);
        var mat2 = _templateManager.GetDbTemplate(dialogBody2);
        var mat3 = _templateManager.GetDbTemplate(dialogBody3);

        var result = new List<TemplateGrayAlpha> { new(mat1), new(mat2), new(mat3) };

        return result;
    }


    private static void MatRemoveErrorInf(ref Mat mat)
    {
        Mat positiveInf = new(mat.Size, mat.Depth, 1);
        Mat negativeInf = new(mat.Size, mat.Depth, 1);

        positiveInf.SetTo(new MCvScalar(1));
        negativeInf.SetTo(new MCvScalar(0));

        var mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, positiveInf, mask, CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);

        mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, negativeInf, mask, CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);
    }

    private Rectangle DialogMatchNameTag(Mat img, int dialogIndex)
    {
        var nameTagTemplate = GetNameTag(ShortName(_names[dialogIndex]));
        var res = LocalMatch(img, nameTagTemplate, 0.7, TemplateMatchingType.CcoeffNormed);

        if (!res.IsEmpty && _nameTagPosition.IsEmpty) _nameTagPosition = new Point(res.X, res.Y);
        return res;

        Rectangle LocalMatch(Mat src, TemplateGrayAlpha tmp, double threshold, TemplateMatchingType matchingType)
        {
            var cropArea = LocalGetCropArea(tmp.Size);
            var imgCropped = new Mat(src, cropArea);

            double minVal = 0, maxVal = 0;
            Point minLoc = new(), maxLoc = new();
            var matchResult = new Mat();

            CvInvoke.MatchTemplate(imgCropped, tmp.Gray, matchResult, matchingType, mask: tmp.Alpha);
            MatRemoveErrorInf(ref matchResult);
            CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
            matchResult.Dispose();

            // if (true && _progressCarrier == null)
            // {
            //     CvInvoke.Imshow("src", imgCropped);
            //     CvInvoke.Imshow("tmp", tmp.Gray);
            //     Console.WriteLine(maxVal);
            //     CvInvoke.WaitKey(1);
            // }

            if (!(threshold < maxVal) || !(maxVal < 1)) return Rectangle.Empty;

            var resPoint = new Point(maxLoc.X + cropArea.X, maxLoc.Y + cropArea.Y);
            return new Rectangle(resPoint, tmp.Size);
        }

        Rectangle LocalGetCropArea(Size ntt)
        {
            var dialogAreaSize = GetDialogAreaSize();
            var rect = new Rectangle
            {
                X = (_videoResolution.Width - dialogAreaSize.Width) / 2,
                Y = (_videoResolution.Height - dialogAreaSize.Height - ntt.Height) / 1,
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
            return _videoResolutionRatio > 16.0 / 9
                ? new Size
                {
                    Height = (int)(0.237 * _videoResolution.Height),
                    Width = (int)(1.389 * _videoResolution.Height)
                }
                : new Size
                {
                    Height = (int)(0.133 * _videoResolution.Width),
                    Width = (int)(0.781 * _videoResolution.Width)
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


        bool LocalMatch(Mat src, TemplateGrayAlpha tmp, double threshold = 0.8)
        {
            double maxVal = 0, minVal = 0;
            Point minLoc = new(), maxLoc = new();
            var offset = _templateManager.DbTemplateMaxSize().Height;
            Rectangle dialogStartPosition = new(
                x: nameTagRect.X + (int)(0.15 * offset),
                y: nameTagRect.Y + (int)(1.2 * offset),
                width: (int)(3.5 * offset),
                height: (int)(1.5 * offset)
            );
            var imgCropped = new Mat(src, dialogStartPosition);
            var matchResult = new Mat();
            CvInvoke.MatchTemplate(imgCropped, tmp.Gray, matchResult,
                TemplateMatchingType.CcoeffNormed, mask: tmp.Alpha);
            MatRemoveErrorInf(ref matchResult);
            CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
            matchResult.Dispose();

            // if (true && _progressCarrier == null)
            // {
            //     CvInvoke.Imshow("src", imgCropped);
            //     CvInvoke.Imshow("tmp", tmp.Gray);
            //     Console.WriteLine(maxVal);
            //     CvInvoke.WaitKey(1);
            // }

            return maxVal > threshold && maxVal < 1;
        }
    }

    private int MatchContentStarted()
    {
        var videoCap = new VideoCapture(_config.VideoFilePath);
        var videoFrame = new Mat();
        var menuSign = new TemplateGrayAlpha(_templateManager.GetMenuSign(), false);
        var frameIndex = 0;
        const double startThreshold = 0.8;

        while (videoCap.Read(videoFrame))
        {
            if (_setStop) return 0;
            CvInvoke.CvtColor(videoFrame, videoFrame, ColorConversion.Bgr2Gray);
            Mat matchResult = new();
            var roi = new Rectangle(
                videoFrame.Width - menuSign.Size.Width * 2, 0,
                menuSign.Size.Width * 2, menuSign.Size.Height * 2
            );
            roi.Extend(0.2);
            Mat frameCropped = new(videoFrame, roi);
            CvInvoke.MatchTemplate(frameCropped, menuSign.Gray, matchResult,
                TemplateMatchingType.CcoeffNormed, mask: menuSign.Alpha);

            MatRemoveErrorInf(ref matchResult);

            double minVal = 0, maxVal = 0;
            Point minLoc = new(), maxLoc = new();
            CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            Log(1);

            if (maxVal > startThreshold) return frameIndex;
            frameIndex++;
        }

        return 0;
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
                Log(ProcessStep);
                needNextFrame = false;
            }

            var frameIndex = FramePos(videoCap);
            if (needNextDialog)
            {
                if (dialogFrameSets.Count == _storyData.Dialogs().Length) break;
                if (dialogFrameSets.Count > 0)
                    Log($"Frame {frameIndex}: Matched Dialogs {dialogFrameSets.Count}/{_storyData.Dialogs().Length}");

                dialogFrameSets.Add(new DialogFrameSet(_storyData.Dialogs()[dialogFrameSets.Count], _videoFrameRate));
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
                // Console.WriteLine(currentDialogStatus);
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

        return dialogFrameSets;

        int FramePos(VideoCapture vc) => (int)vc.Get(CapProp.PosFrames);
    }

    // private List<BannerFrameSet> MatchBanners(int startPosition = 0)
    // {
    //     var videoCap = new VideoCapture(_config.VideoFilePath);
    //     var videoFrame = new Mat();
    //     var bannerIndex = 0;
    //     var nextFrame = true;
    //     var frameIndex = startPosition;
    //     BannerFrameSet? frameList = null;
    //     var bannerList = new List<BannerFrameSet>();
    //
    //     videoCap.Set(Emgu.CV.CvEnum.CapProp.PosFrames, frameIndex);
    //     while (true)
    //     {
    //         if (bannerIndex >= _storyData.Banners().Length) break;
    //         frameList ??= new BannerFrameSet(_storyData.Banners()[bannerIndex]);
    //         if (nextFrame)
    //         {
    //             var readResult = videoCap.Read(videoFrame);
    //             if (!readResult) break;
    //             CvInvoke.CvtColor(videoFrame, videoFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
    //             nextFrame = false;
    //             frameIndex++;
    //         }
    //         Log(frameIndex,
    //             $"Frame {frameIndex}: Processing Banners {bannerIndex + 1}/{_storyData.Banners().Length}"
    //         );
    //         if (bannerList.Count == _storyData.Banners().Length) break;
    //     }
    //
    //     return bannerList;
    // } // TODO : Match Banner

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
        // var alphaTagTransparent = @"{\alphaFF}";
        var queue = FormatDialogBodyArr(body);
        var fadeTime = _config.TyperSetting.FadeTime;
        var charTime = _config.TyperSetting.CharTime;
        if (fadeTime <= 0 && charTime <= 0)
            return string.Join("", queue);

        var nowTime = (int)(1000 / _videoFrameRate.Fps() * frameCount);
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

    private List<SubtitleStyleItem> MakeDialogStyles()
    {
        var fontsize = (int)((_videoResolutionRatio > 16.0 / 9
            ? _videoResolution.Height * 0.043
            : _videoResolution.Width * 0.024) * (70 / 61D));

        var outlineSize = (int)Math.Ceiling(fontsize / 15.0);
        var marginV = _nameTagPosition.Y + (int)(fontsize * 2.3);
        var marginH = _nameTagPosition.X + (int)(fontsize * 0.4);

        var charaFontsize = (int)(fontsize * 0.9);
        var charaOutlineSize = (int)Math.Ceiling(charaFontsize / 15.0);
        const string fontName = "思源黑体 CN Bold";

        var blackColor = new SubtitleColor(255, 255, 255).ToString();
        var outlineColor = new SubtitleColor(73, 71, 102, 50).ToString();
        var result = new List<SubtitleStyleItem>
        {
            new("Line1", fontName, fontsize, primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH, marginV: marginV),
            new("Line2", fontName, fontsize, primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01)),
            new("Line3", fontName, fontsize, primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01 * 2)),
            new("Character", fontName, charaFontsize, primaryColour: blackColor, outlineColour: outlineColor,
                outline: charaOutlineSize, shadow: 0, alignment: 7),
            new("Screen", fontName, charaFontsize, primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7)
        };

        return result;
    }

    private List<SubtitleEventItem> MakeDialogEvents(List<DialogFrameSet> dialogList)
    {
        var result = new List<SubtitleEventItem>();
        var needSepCount = new List<RequestItem>();
        foreach (var set in dialogList)
        {
            var needForceReturn =
                set.DialogData.BodyOriginal.Split("\n").Length == 3 && (
                    set.DialogData.FinalContent.Contains("\\R") ||
                    set.DialogData.FinalContent.Replace("\n", "").Length > 37
                );
            if (!needForceReturn) continue;
            var item = new RequestItem(set.DialogData.FinalContent, set.StartIndex(), set.EndIndex(),
                _videoFrameRate.Fps());
            needSepCount.Add(item);
        }

        Report(new TaskLogRequest(needSepCount));
        if (_progressCarrier != null)
        {
            while (_separateLineCollection.Count < needSepCount.Count) Thread.Sleep(100);
        }

        var dialogIndex = 0;
        foreach (var set in dialogList)
        {
            dialogIndex++;

            if (_progressCarrier != null && _separateLineCollection.TryGetValue(set.StartIndex(), out var info))
            {
                var items = SeparateDialogSet(set, info);
                result.AddRange(GenerateDialogEvent(items[0]));
                result.AddRange(GenerateDialogEvent(items[1]));
            }
            else
            {
                result.AddRange(GenerateDialogEvent(set));
            }

            result.Add(SubtitleEventItem.Comment($"-----  {dialogIndex:000}  -----",
                set.StartTime(), set.EndTime(), "Screen"));
        }

        return result;


        List<DialogFrameSet> SeparateDialogSet(DialogFrameSet dialogFrameSet, LineSepInfo info)
        {
            var content = dialogFrameSet.DialogData.FinalContent
                .Replace("\n", "")
                .Replace("\\N", "")
                .Replace("\\R", "");

            var sepCount = info.Frame - dialogFrameSet.StartIndex();

            var sepSet1 = new DialogFrameSet((StoryDialogEvent)dialogFrameSet.DialogData.Clone(), _videoFrameRate);
            var sepSet2 = new DialogFrameSet((StoryDialogEvent)dialogFrameSet.DialogData.Clone(), _videoFrameRate);

            sepSet1.Frames.AddRange(dialogFrameSet.Frames[..sepCount]);
            sepSet2.Frames.AddRange(dialogFrameSet.Frames[sepCount..]);

            sepSet1.DialogData.BodyTranslated = content[..info.Substr];
            sepSet2.DialogData.BodyTranslated = content[info.Substr..];

            return [sepSet1, sepSet2];
        }

        IEnumerable<SubtitleEventItem> GenerateDialogEvent(DialogFrameSet set)
        {
            var subtitleEventItems = new List<SubtitleEventItem>();
            subtitleEventItems.AddRange(set.IsJitter
                ? GenerateJitterDialogEvents(set)
                : GenerateNoneJitterDialogEvents(set));
            return subtitleEventItems;
        }

        IEnumerable<SubtitleEventItem> GenerateNoneJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.DialogData.FinalContent;
            var characterName = dialogFrameSet.DialogData.FinalCharacter;
            var originLineCount = dialogFrameSet.DialogData.BodyOriginal.Split("\n").Length;
            var styleName = "Line" + originLineCount;

            var startTime = dialogFrameSet.StartTime();
            var endTime = dialogFrameSet.EndTime();

            var body = MakeDialogTypewriter(content);

            var dialogItem = SubtitleEventItem.Dialog(body, startTime, endTime, styleName);

            var characterItemPosition =
                dialogFrameSet.Start().Point +
                new Size(GetNameTag(dialogFrameSet.DialogData.CharacterOriginal).Size.Width + 10, 0);
            var characterItemPositionTag = $@"{{\pos({characterItemPosition.X},{characterItemPosition.Y})}}";
            var characterItem = SubtitleEventItem.Dialog(characterItemPositionTag + characterName,
                startTime, endTime, "Character");
            if (characterName == "") characterItem = characterItem.ToComment();
            var returnVal = new List<SubtitleEventItem> { characterItem, dialogItem };

            return returnVal;
        }

        IEnumerable<SubtitleEventItem> GenerateJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.DialogData.FinalContent;
            var characterName = dialogFrameSet.DialogData.FinalCharacter;
            var originLineCount = dialogFrameSet.DialogData.BodyOriginal.Split("\n").Length;

            var styleName = "Line" + originLineCount;
            var styles = MakeDialogStyles();
            var style = styles.Find(s => s.Name == styleName)!;

            var constPosition = dialogFrameSet.Start().Point;
            var lastPosition = new Point(0, 0);
            var dialogEvents = new List<SubtitleEventItem>();
            var characterEvents = new List<SubtitleEventItem>();
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
                    var dialogItem = SubtitleEventItem.Dialog(body, frame.StartTime(), frame.EndTime(), styleName);
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

                    var characterItem = SubtitleEventItem.Dialog(
                        tag + characterName, frame.StartTime(), frame.EndTime(), "Character");
                    if (characterName == "") characterItem = characterItem.ToComment();
                    characterEvents.Add(characterItem);
                }

                lastPosition = new Point(x, y);
            }

            var returnVal = new List<SubtitleEventItem>();
            returnVal.AddRange(dialogEvents);
            returnVal.AddRange(characterEvents);
            return returnVal;
        }
    }

    private bool _setStop;

    public void Stop()
    {
        _setStop = true;
    }

    public string Process()
    {
        var subtitleEventItems = new List<SubtitleEventItem>();
        try
        {
            Log("Start To Process");
            var contentStartedAt = MatchContentStarted();
            Log($"Video Content Start At Frame {contentStartedAt}");
            var dialogMatchResult = MatchDialogs(contentStartedAt);
            var dialogEvents = MakeDialogEvents(dialogMatchResult);
            Log(_setStop ? "Process Aborted" : "Dialog Match Finished");

            subtitleEventItems.AddRange(dialogEvents);
        }
        catch (Exception e)
        {
            Log(e);
        }
        finally
        {
            if (!_setStop)
            {
                Write(subtitleEventItems);
                Log("Process Finished");
            }
            else
            {
                Log("Process Stopped");
            }

            ReportFinished();
        }

        return _setStop ? "" : _config.OutputFilePath;

        void Write(List<SubtitleEventItem> eventList)
        {
            var scriptInfo = new SubtitleScriptInfo(
                _videoResolution.Width, _videoResolution.Height,
                Path.GetFileNameWithoutExtension(_config.VideoFilePath));
            var garbage = new SubtitleGarbage(_config.VideoFilePath, _config.VideoFilePath);
            var style = new SubtitleStyles(MakeDialogStyles().ToArray());

            var events = new SubtitleEvents(eventList.ToArray());
            var assData = new Subtitle(scriptInfo, garbage, style, events);
            var outputFile = new StreamWriter(_config.OutputFilePath, false, Encoding.UTF8);
            outputFile.Write(assData.ToString());
            outputFile.Close();
        }
    }
}
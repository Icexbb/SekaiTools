using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.IO;
using Emgu.CV;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace SekaiTools.source;

public class VideoProcess
{
    private struct TemplateGrayAlpha
    {
        public Mat Gray;
        public Mat Alpha;
        public Size Size => Gray.Size;
    }

    private class DialogFrameResult(int frameIndex, int dialogIndex, int dialogStatus, Rectangle nameTagRect)
    {
        public readonly int FrameIndex = frameIndex;
        public readonly int DialogIndex = dialogIndex;
        public readonly int DialogStatus = dialogStatus;
        public readonly Rectangle NameTagRect = nameTagRect;
        public Point Point => NameTagRect.Location;
        public TimeSpan FrameTime(double fps) => TimeSpan.FromMilliseconds(FrameIndex * (1000 / fps));
        public string FrameTimeStr(double fps) => FrameTime(fps).ToString(@"hh\:mm\:ss\.ff");

        public string FrameEndTimeStr(double fps) => FrameTime(fps)
            .Add(TimeSpan.FromMilliseconds(1000 / fps))
            .ToString(@"hh\:mm\:ss\.ff");
    }

    private class DialogFrameSet
    {
        public readonly List<DialogFrameResult> Data = new();
        public bool IsEmpty => Data.Count == 0;

        public bool IsJitter => Data.Select(
            v => Distance(Data[0].Point, v.Point) < Math.Sqrt(2)).Count() != Data.Count;

        public StoryDialogEvent? Dialog;
        public string StartTime(double fps) => Data[0].FrameTimeStr(fps);
        public string EndTime(double fps) => Data[^1].FrameTimeStr(fps);
    }

    private static double Distance(Point a, Point b)
    {
        return Math.Sqrt((a.X - b.X) ^ 2 + (a.Y - b.Y) ^ 2);
    }

    private readonly string _videoFilePath;
    private readonly string _outputFilePath;

    private readonly StoryData _storyData;

    private readonly Size _videoResolution;
    private readonly double _videoResolutionRatio;
    private readonly double _videoFrameRate;
    private readonly int _videoFrameCount;
    private TemplateGrayAlpha? _menuSign;
    private readonly Dictionary<string, TemplateGrayAlpha> _nameTagMats;
    private Size _nameTagMaxSize;
    private Point _nameTagPosition;
    private readonly List<string> _names;
    private readonly List<TemplateGrayAlpha[]> _dialogIdentifierMats;

    public VideoProcess(string videoFilePath, string jsonFilePath, string translateFilePath)
    {
        if (!Path.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found.", videoFilePath);
        if (!Path.Exists(jsonFilePath))
            throw new FileNotFoundException("Json file not found.", jsonFilePath);
        _videoFilePath = videoFilePath;
        _outputFilePath = Path.Join(Path.GetDirectoryName(videoFilePath),
            Path.GetFileNameWithoutExtension(videoFilePath) + ".ass");

        _storyData = StoryData.FromFile(jsonFilePath, translateFilePath);
        var videoCap = new VideoCapture(videoFilePath);
        var videoFrame = new Mat();
        videoCap.Read(videoFrame);
        _videoResolution = videoFrame.Size;
        _videoResolutionRatio = (double)_videoResolution.Width / _videoResolution.Height;
        _videoFrameRate = videoCap.Get(Emgu.CV.CvEnum.CapProp.Fps);
        _videoFrameCount = (int)videoCap.Get(Emgu.CV.CvEnum.CapProp.FrameCount);
        videoFrame.Dispose();
        videoCap.Dispose();

        _nameTagMats = new Dictionary<string, TemplateGrayAlpha>();
        _dialogIdentifierMats = new List<TemplateGrayAlpha[]>();
        _names = new List<string>();
        _nameTagMaxSize = new Size(0, 0);
        _nameTagPosition = new Point(0, 0);
        foreach (var dialog in _storyData.Dialogs()) _names.Add(dialog.CharacterOriginal);
        GetTemplate();
    }

    private static void CallPythonScriptToGenerate(string transferData)
    {
        var pythonScriptPath = Path.Join(Directory.GetCurrentDirectory(), "scripts", "GenTextPic.py");
        var pythonExePath = Path.Join(Directory.GetCurrentDirectory(), "runtime", "python-3.12.0", "python.exe");
        pythonScriptPath = $"\"{pythonScriptPath}\"";
        var p = new Process();
        p.StartInfo.FileName = pythonExePath; //需要执行的文件路径
        p.StartInfo.UseShellExecute = false; //必需
        p.StartInfo.RedirectStandardOutput = true; //输出参数设定
        p.StartInfo.RedirectStandardInput = true; //传入参数设定
        p.StartInfo.RedirectStandardError = true; //错误信息设定
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.Arguments = $"{pythonScriptPath} {Convert.ToBase64String(Encoding.UTF8.GetBytes(transferData))}";
        p.Start();
        // var output = p.StandardOutput.ReadToEnd();
        // var decodedString = Encoding.UTF8.GetString(Convert.FromBase64String(output));
        p.WaitForExit(); //关键，等待外部程序退出后才能往下执行}
        p.Close();
        // return decodedString;
    }

    private void GenerateEbString(string str)
    {
        var font = Path.Join(Directory.GetCurrentDirectory(), "fonts", "FOT-RodinNTLGPro-EB.otf");
        var imgWidth = _videoResolution.Width;
        var imgHeight = _videoResolution.Height;
        var transferData = Newtonsoft.Json.JsonConvert.SerializeObject(new { str, imgWidth, imgHeight, font });
        CallPythonScriptToGenerate(transferData);
    }

    private void GenerateDbString(string str)
    {
        var font = Path.Join(Directory.GetCurrentDirectory(), "fonts", "FOT-RodinNTLGPro-DB.otf");
        var imgWidth = _videoResolution.Width;
        var imgHeight = _videoResolution.Height;
        var transferData = Newtonsoft.Json.JsonConvert.SerializeObject(new { str, imgWidth, imgHeight, font });
        CallPythonScriptToGenerate(transferData);
    }

    private TemplateGrayAlpha GenerateNameTag(string name)
    {
        var imgWidth = _videoResolution.Width;
        var imgHeight = _videoResolution.Height;
        var nameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(name))
            .Replace('\\', '_').Replace('/', '_');
        var predictedFilename = $"{nameB64}.png";
        var predictedDirPath = Path.Join(
            Directory.GetCurrentDirectory(), "patterns", $"{imgWidth}x{imgHeight}", "EB");
        if (!Directory.Exists(predictedDirPath)) Directory.CreateDirectory(predictedDirPath);
        var predictedFilepath = Path.Join(predictedDirPath, predictedFilename);

        if (!Path.Exists(predictedFilepath)) GenerateEbString(name);

        var nameTag = CvInvoke.Imread(predictedFilepath, Emgu.CV.CvEnum.ImreadModes.Unchanged);
        var result = ExtractGrayAlpha(nameTag);
        nameTag.Dispose();
        if (result.Size.Width > _nameTagMaxSize.Width) _nameTagMaxSize.Width = result.Size.Width;
        if (result.Size.Height > _nameTagMaxSize.Height) _nameTagMaxSize.Height = result.Size.Height;
        return result;
    }

    private List<TemplateGrayAlpha> GenerateDialogBegin(char char1, char char2)
    {
        var imgWidth = _videoResolution.Width;
        var imgHeight = _videoResolution.Height;
        var result = new List<TemplateGrayAlpha>();
        foreach (var str in new[] { $"{char1}", $"{char1}{char2}" })
        {
            var contentB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(str))
                .Replace('\\', '_').Replace('/', '_');
            var predictedFilename = $"{contentB64}.png";
            var predictedDirPath = Path.Join(
                Directory.GetCurrentDirectory(), "patterns", $"{imgWidth}x{imgHeight}", "DB"
            );
            if (!Directory.Exists(predictedDirPath)) Directory.CreateDirectory(predictedDirPath);
            var predictedFilepath = Path.Join(predictedDirPath, predictedFilename);
            if (!Path.Exists(predictedFilepath)) GenerateDbString(str);
            var srcImg = CvInvoke.Imread(predictedFilepath, Emgu.CV.CvEnum.ImreadModes.Unchanged);
            var extracted = ExtractGrayAlpha(srcImg);

            srcImg.Dispose();
            result.Add(extracted);
        }

        var diff = result[1].Size.Width - result[0].Size.Width;
        CvInvoke.CopyMakeBorder(result[0].Gray, result[0].Gray, 0, 0, 0, diff,
            Emgu.CV.CvEnum.BorderType.Constant, new MCvScalar(0));
        CvInvoke.CopyMakeBorder(result[0].Alpha, result[0].Alpha, 0, 0, 0, diff,
            Emgu.CV.CvEnum.BorderType.Constant, new MCvScalar(255));
        return result;
    }

    private static TemplateGrayAlpha ExtractGrayAlpha(IInputArray src, bool resize = true)
    {
        var grayImage = new Mat();
        var alphaChannel = new Mat();
        CvInvoke.CvtColor(src, grayImage, Emgu.CV.CvEnum.ColorConversion.Bgra2Gray);
        CvInvoke.ExtractChannel(src, alphaChannel, 3);
        if (!resize) return new TemplateGrayAlpha { Gray = grayImage, Alpha = alphaChannel };

        var scaleRatio = 5;
        CvInvoke.Resize(grayImage, grayImage,
            new Size(grayImage.Size.Width / scaleRatio, grayImage.Size.Height / scaleRatio));
        CvInvoke.Resize(alphaChannel, alphaChannel,
            new Size(alphaChannel.Size.Width / scaleRatio, alphaChannel.Size.Height / scaleRatio));
        return new TemplateGrayAlpha { Gray = grayImage, Alpha = alphaChannel };
    }

    private void GetTemplate()
    {
        Console.WriteLine($"Generating template for {_videoFilePath}");
        var nameList = new List<string>();
        var dialogList = new List<string>();
        foreach (var dialog in _storyData.Dialogs())
        {
            if (!nameList.Contains(dialog.CharacterOriginal)) nameList.Add(dialog.CharacterOriginal);
            dialogList.Add(dialog.BodyOriginal[..2]);
        }

        foreach (var name in nameList)
        {
            _nameTagMats.Add(name, GenerateNameTag(name));
        }

        foreach (var dia in dialogList)
        {
            _dialogIdentifierMats.Add(GenerateDialogBegin(dia[0], dia[1]).ToArray());
        }
    }

    private TemplateGrayAlpha GetMenuSign()
    {
        if (_menuSign != null) return (TemplateGrayAlpha)_menuSign;
        var menuTemplatePath = Path.Join(Directory.GetCurrentDirectory(), "patterns", "menu-107px.png");
        if (!File.Exists(menuTemplatePath)) throw new FileNotFoundException();
        var menuTemplate = CvInvoke.Imread(menuTemplatePath, Emgu.CV.CvEnum.ImreadModes.Unchanged);
        int menuSize;
        if (_videoResolutionRatio > 16.0 / 9)
            menuSize = (int)(_videoResolution.Height * 0.0741);
        else
            menuSize = (int)(_videoResolution.Width * 0.0417);

        CvInvoke.Resize(menuTemplate, menuTemplate, new Size(menuSize, menuSize));
        var result = ExtractGrayAlpha(menuTemplate, false);
        _menuSign = result;
        return result;
    }

    private TemplateGrayAlpha GetNameTag(string name)
    {
        if (!_nameTagMats.ContainsKey(name))
        {
            _nameTagMats.Add(name, GenerateNameTag(name));
        }

        _nameTagMats.TryGetValue(name, out var result);
        return result;
    }

    private List<TemplateGrayAlpha> GetDialogInd(int index)
    {
        if (_dialogIdentifierMats.Count > index) return _dialogIdentifierMats[index].ToList();
        if (_storyData.Dialogs().Length <= index) throw new IndexOutOfRangeException();
        var dialog = _storyData.Dialogs()[index];
        _dialogIdentifierMats[index] =
            GenerateDialogBegin(dialog.BodyOriginal[0], dialog.BodyOriginal[1]).ToArray();

        return _dialogIdentifierMats[index].ToList();
    }

    private Size GetDialogAreaSize()
    {
        return _videoResolutionRatio > 16.0 / 9
            ? new Size
            {
                Height = (int)(0.237 * _videoResolution.Height), Width = (int)(1.389 * _videoResolution.Height)
            }
            : new Size
            {
                Height = (int)(0.133 * _videoResolution.Width), Width = (int)(0.781 * _videoResolution.Width)
            };
    }

    private static void MatRemoveErrorInf(ref Mat mat)
    {
        Mat positiveInf = new(mat.Size, mat.Depth, 1);
        Mat negativeInf = new(mat.Size, mat.Depth, 1);

        positiveInf.SetTo(new MCvScalar(float.PositiveInfinity));
        negativeInf.SetTo(new MCvScalar(float.NegativeInfinity));

        var mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, positiveInf, mask, Emgu.CV.CvEnum.CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);

        mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, negativeInf, mask, Emgu.CV.CvEnum.CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);
    }

    private void SiftMatch(Mat src, Mat template)
    {
        var srcImg1 = src;
        var srcImg2 = template;

        // CvInvoke.Imshow("src1", srcImg1);
        // CvInvoke.Imshow("src2", srcImg2);

        SIFT sift = new SIFT();
        //计算特征点
        var keyPoints1 = sift.Detect(srcImg1);
        var keyPoints2 = sift.Detect(srcImg2);
        //绘制特征点
        // var sift_feature1 = new Mat();
        // var sift_feature2 = new Mat();
        var vkeyPoint1 = new VectorOfKeyPoint(keyPoints1);
        var vkeyPoint2 = new VectorOfKeyPoint(keyPoints2);
        // Features2DToolbox.DrawKeypoints(srcImg1, vkeyPoint1, sift_feature1, new Bgr(0, 255, 0));
        // Features2DToolbox.DrawKeypoints(srcImg2, vkeyPoint2, sift_feature2, new Bgr(0, 255, 0));
        //显示绘制结果
        // CvInvoke.Imshow("sift_feature1", sift_feature1);
        // CvInvoke.Imshow("sift_feature2", sift_feature2);
        //计算特征描述符
        var descriptors1 = new Mat();
        var descriptors2 = new Mat();
        sift.Compute(srcImg1, vkeyPoint1, descriptors1);
        sift.Compute(srcImg2, vkeyPoint2, descriptors2);
        //使用BF匹配器进行暴力匹配
        var bFMatcher = new BFMatcher(DistanceType.L2);
        var matches = new VectorOfVectorOfDMatch();
        //添加特征描述符
        bFMatcher.Add(descriptors1);
        //k最邻近匹配
        bFMatcher.KnnMatch(descriptors2, matches, 2);
        //寻找匹配结果中距离的最值
        double min_dist = 100, max_dist = 0;
        for (int i = 0; i < descriptors1.Rows; i++)
        {
            try
            {
                if (matches[i][0].Distance > max_dist)
                {
                    max_dist = matches[i][0].Distance;
                }

                if (matches[i][0].Distance < min_dist)
                {
                    min_dist = matches[i][0].Distance;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error");
            }
        }

        //对BF匹配结果进行筛选
        VectorOfVectorOfDMatch good_matches = new VectorOfVectorOfDMatch();
        for (int i = 0; i < matches.Size; i++)
        {
            // Console.WriteLine(matches[i].Length);
            if (matches[i].Length < 2) continue;
            //符合条件的匹配点进行存储
            if (matches[i][0].Distance < 1.5 * min_dist)
            {
                good_matches.Push(matches[i]);
            }
        }

        //绘制匹配点
        Mat result = new Mat();
        Features2DToolbox.DrawMatches(
            srcImg1, vkeyPoint1, srcImg2, vkeyPoint2,
            good_matches, result,
            new MCvScalar(0, 255, 0), new MCvScalar(0, 0, 255), null,
            Features2DToolbox.KeypointDrawType.NotDrawSinglePoints);
        //显示匹配结果
        CvInvoke.Imshow("match-result", result);

        CvInvoke.WaitKey(1);
    }

    private Rectangle MatchNameTag(Mat img, string name)
    {
        var nameTagTemplate = GetNameTag(name);
        var dialogAreaSize = GetDialogAreaSize();
        var nameTagSize = nameTagTemplate.Size;

        var cropArea = new Rectangle
        {
            Height = (int)(nameTagSize.Height * 1.6),
            Width = nameTagSize.Width * 2,
            X = _videoResolution.Width / 2 - dialogAreaSize.Width / 2 - nameTagSize.Width / 2 +
                (int)(nameTagSize.Height * 0.4),
            Y = _videoResolution.Height - dialogAreaSize.Height - nameTagSize.Height / 1
        };
        if (cropArea.X < 0) cropArea.X = 0;
        if (cropArea.Y < 0) cropArea.Y = 0;
        if (cropArea.X + cropArea.Width > img.Size.Width) cropArea.Width = img.Size.Width - cropArea.X;
        if (cropArea.Y + cropArea.Height > img.Size.Height) cropArea.Height = img.Size.Height - cropArea.Y;

        var imgCropped = new Mat(img, cropArea);
        // SiftMatch(imgCropped, nameTagTemplate.Gray);

        double ccoeffNormedResult = 0;
        double minVal = 0;
        Point minLoc = new(), maxLoc = new();

        Mat matchResult = new();
        CvInvoke.MatchTemplate(imgCropped, nameTagTemplate.Gray, matchResult,
            Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed, mask: nameTagTemplate.Alpha);
        MatRemoveErrorInf(ref matchResult);
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref ccoeffNormedResult, ref minLoc, ref maxLoc);

        switch (ccoeffNormedResult)
        {
            case > 0.75:
            {
                var resPoint = new Point(maxLoc.X + cropArea.X, maxLoc.Y + cropArea.Y);
                _nameTagPosition = resPoint;
                return new Rectangle(resPoint, nameTagTemplate.Size);
            }
            case > 0.45:
            {
                double ccorrNormedResult = 0;

                CvInvoke.MatchTemplate(imgCropped, nameTagTemplate.Gray, matchResult,
                    Emgu.CV.CvEnum.TemplateMatchingType.CcorrNormed, mask: nameTagTemplate.Alpha);
                MatRemoveErrorInf(ref matchResult);
                CvInvoke.MinMaxLoc(matchResult, ref minVal, ref ccorrNormedResult, ref minLoc, ref maxLoc);
                // Console.WriteLine($"ccorr {ccorrNormedResult:0.00} ccoeff {ccoeffNormedResult:0.00}");
                if (!(ccorrNormedResult > 0.8)) return new Rectangle(0, 0, 0, 0);

                var resPoint = new Point(maxLoc.X + cropArea.X, maxLoc.Y + cropArea.Y);
                _nameTagPosition = resPoint;
                return new Rectangle(resPoint, nameTagTemplate.Size);
            }
            default:
                return new Rectangle(0, 0, 0, 0);
        }
    }

    private int MatchDialog(Mat img, int index, Rectangle nameTagRect, int lastStatus = 0)
    {
        if (nameTagRect.X == 0) return 0;
        var charTemplates = GetDialogInd(index);
        var template1 = charTemplates[0];
        var template2 = charTemplates[1];

        Rectangle dialogStartPosition = new(
            nameTagRect.X,
            nameTagRect.Y + _nameTagMaxSize.Height,
            (int)(1.6 * template2.Size.Width),
            (int)(1.6 * _nameTagMaxSize.Height)
        );
        Mat imgCropped = new(img, dialogStartPosition);
        Mat matchResult = new();

        double minVal = 0, maxVal1 = 0, maxVal2 = 0;
        Point minLoc = new(), maxLoc = new();
        const double char2Threshold = 0.6;
        const double char1Threshold = 0.6;

        CvInvoke.MatchTemplate(imgCropped, template2.Gray, matchResult,
            Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed, mask: template2.Alpha);
        MatRemoveErrorInf(ref matchResult);
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal2, ref minLoc, ref maxLoc);
        if (maxVal2 > char2Threshold) return 2;
        if (lastStatus == 2) return 0;
        CvInvoke.MatchTemplate(imgCropped, template1.Gray, matchResult,
            Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed, mask: template1.Alpha);
        MatRemoveErrorInf(ref matchResult);
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal1, ref minLoc, ref maxLoc);
        return maxVal1 > char1Threshold ? 1 : 0;
    }

    private int MatchContentStarted()
    {
        var videoCap = new VideoCapture(_videoFilePath);
        var videoFrame = new Mat();
        var menuSign = GetMenuSign();
        var frameIndex = 0;
        var startThreshold = 0.8;
        var fps = 0.0;
        var sw = new Stopwatch();

        while (videoCap.Read(videoFrame))
        {
            sw.Restart();
            CvInvoke.CvtColor(videoFrame, videoFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            Mat matchResult = new();
            Mat frameCropped = new(videoFrame, new Rectangle(
                videoFrame.Width - menuSign.Size.Width * 2,
                0, // videoFrame.Height - menuSign.Size.Height * 2,
                menuSign.Size.Width * 2,
                menuSign.Size.Height * 2
            ));
            CvInvoke.MatchTemplate(frameCropped, menuSign.Gray, matchResult,
                Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed, mask: menuSign.Alpha);

            MatRemoveErrorInf(ref matchResult);

            double minVal = 0, maxVal = 0;
            Point minLoc = new(), maxLoc = new();
            CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            sw.Stop();
            if (fps == 0.0) fps = 1000.0 / sw.ElapsedMilliseconds;
            else fps = fps / 2 + 1000.0 / sw.ElapsedMilliseconds / 2;

            Console.WriteLine($"FPS {fps:0.00} Frame {frameIndex} maxVal {maxVal}");
            if (maxVal > startThreshold)
                return frameIndex;
            frameIndex++;
        }

        return 0;
    }

    private List<DialogFrameSet> MatchDialogs(int startPosition = 0)
    {
        var videoCap = new VideoCapture(_videoFilePath);
        var videoFrame = new Mat();
        var dialogIndex = 0;
        var dialogStatus = 0;
        var nextFrame = true;
        var frameIndex = startPosition;
        var frameList = new DialogFrameSet();
        var dialogList = new List<DialogFrameSet>();
        var indexIncreased = true;
        var fps = 0.0;
        var sw = new Stopwatch();
        videoCap.Set(Emgu.CV.CvEnum.CapProp.PosFrames, frameIndex);
        while (true)
        {
            if (dialogIndex >= _storyData.Dialogs().Length) break;
            sw.Restart();
            if (nextFrame)
            {
                var readResult = videoCap.Read(videoFrame);
                if (!readResult) break;
                CvInvoke.CvtColor(videoFrame, videoFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
                nextFrame = false;
                frameIndex++;
            }

            var nameTagRect = MatchNameTag(videoFrame, _names[dialogIndex]);
            if (nameTagRect.Height == 0)
            {
                if (frameList.Data.Count != 0)
                {
                    dialogList.Add(frameList);
                    frameList = new DialogFrameSet();
                }

                if (!indexIncreased)
                {
                    dialogIndex++;
                    indexIncreased = true;
                    dialogStatus = 0;
                }
                else nextFrame = true;
            }
            else
            {
                var currentDialogStatus = MatchDialog(videoFrame, dialogIndex, nameTagRect, dialogStatus);

                if (currentDialogStatus != 0)
                {
                    var currentDialogFrameResult = new DialogFrameResult(
                        frameIndex, dialogIndex,
                        currentDialogStatus, nameTagRect
                    );
                    frameList.Data.Add(currentDialogFrameResult);
                    frameList.Dialog ??= _storyData.Dialogs()[dialogIndex];
                    nextFrame = true;
                    indexIncreased = false;
                }
                else // currentDialogStatus == 0
                {
                    if (dialogStatus == 2)
                    {
                        if (frameList.Data.Count != 0)
                        {
                            dialogList.Add(frameList);
                            frameList = new DialogFrameSet();
                        }

                        dialogIndex++;
                        indexIncreased = true;
                    }
                    else //dialogStatus == 0
                    {
                        nextFrame = true;
                    }
                }

                dialogStatus = indexIncreased ? 0 : currentDialogStatus;
            }

            sw.Stop();
            if (fps == 0.0) fps = 1000.0 / sw.ElapsedMilliseconds;
            else fps = fps / 2 + 1000.0 / sw.ElapsedMilliseconds / 2;

            Console.WriteLine(
                $"FPS {fps:0.00} Frame {frameIndex + 1}/{_videoFrameCount} Dialog {dialogIndex}/{_storyData.Dialogs().Length} Status {dialogStatus}");

            if (dialogList.Count == _storyData.Dialogs().Length) break;
        }

        return dialogList;
    }

    private static string[] FormatDialogBodyArr(string body)
    {
        string[] returnChar = { "\n", "\\n", "\\N" };
        var bodyCopy = returnChar.Aggregate(body, (current, s) => current.Replace(s, "\n"));
        var lineCount = bodyCopy.Select(c => c == '\n').Count();
        if (lineCount == 2) bodyCopy = bodyCopy.Replace("\n", "");

        var bodyArr = new List<string>();
        var t = "";
        foreach (var c in bodyCopy)
        {
            if (c == '.')
            {
                if (t is "" or "." or "..")
                {
                    t += c;
                    if (t != "...") continue;
                    bodyArr.Add("…");
                    t = "";
                }
                else bodyArr.Add(c.ToString());
            }
            else
            {
                if (t is "" or "." or "..")
                {
                    bodyArr.AddRange(t.Select(dc => dc.ToString()));
                    t = "";
                }

                bodyArr.Add(c.ToString());
            }
        }

        return bodyArr.ToArray();
    }

    private string MakeDialogTyper(string body, int[] typerInterval)
    {
        var bodyArr = FormatDialogBodyArr(body);

        var res = "";
        var nextStart = 0;
        var fadeTime = typerInterval[0];
        var charTime = typerInterval[1];
        foreach (var s in bodyArr)
        {
            var r = "";
            var start = 0;
            if (fadeTime > 0 && charTime > 0)
            {
                start = nextStart;
                var end = start + fadeTime;
                if (s == "\n") start += 300;
                r += $@"{{\alphaFF\t({start},{end},1,\alpha0)}}";
            }

            if (s == "\n") r += "\\N";
            else r += s;
            res += r;
            nextStart = start + charTime;
        }

        return res;
    }

    private string MakeDialogTyper(string body, int[] typerInterval, int frameCount)
    {
        const string alphaTagTransparent = @"{\alphaFF}";

        var bodyArr = FormatDialogBodyArr(body);
        var frameTimeMs = 1000 / _videoFrameRate;
        var nowTime = (int)(frameTimeMs * frameCount * 1000);
        var isTransparentNow = false;
        var characterTimeNow = 0;
        var fadeTime = typerInterval[0];
        var charTime = typerInterval[1];
        var sb = new StringBuilder();
        foreach (var s in bodyArr)
        {
            var c = s;
            var alphaTag = "";
            characterTimeNow += charTime;
            if (c == "\n") characterTimeNow += 300;
            if (characterTimeNow < nowTime && nowTime < characterTimeNow + fadeTime)
            {
                alphaTag = $@"{{\alpha{(nowTime - characterTimeNow) / fadeTime * 255}}}"; // 0-255
            }
            else if (characterTimeNow > nowTime)
            {
                if (!isTransparentNow)
                {
                    alphaTag = alphaTagTransparent;
                    isTransparentNow = true;
                }
            }

            if (c == "\n") c = "\\N";
            if (fadeTime > 0 && charTime > 0) sb.Append(alphaTag);

            sb.Append(c);
        }

        return sb.ToString();
    }

    private List<SubtitleStyleItem> MakeDialogStyles()
    {
        var fontsize = (int)(_videoResolutionRatio > 16.0 / 9
            ? _videoResolution.Height * 0.043
            : _videoResolution.Width * 0.024);

        var outlineSize = (int)Math.Ceiling(fontsize / 15.0);
        var marginV = _nameTagPosition.Y + (int)(_nameTagMaxSize.Height * 2.4);
        var marginH = _nameTagPosition.X + (int)(_nameTagMaxSize.Height * 0.6);

        var charaFontsize = (int)(fontsize * 0.9);
        var charaOutlineSize = (int)Math.Ceiling(charaFontsize / 15.0);
        var result = new List<SubtitleStyleItem>
        {
            new(
                "Line1", "思源黑体 CN Bold", fontsize,
                "&H00FFFFFF", "&H000000FF", "&H32664749", "&H00000000",
                0, 0, 0, 0,
                100, 100,
                0, 0, 1, outlineSize, 0, 7,
                marginH, marginH, marginV, 1
            ),
            new(
                "Line2", "思源黑体 CN Bold", fontsize,
                "&H00FFFFFF", "&H000000FF", "&H32664749", "&H00000000",
                0, 0, 0, 0,
                100, 100,
                0, 0, 0, outlineSize, 0, 7,
                marginH, marginH, marginV + (int)(fontsize * 1.2), 1
            ),
            new(
                "Line3", "思源黑体 CN Bold", fontsize,
                "&H00FFFFFF", "&H000000FF", "&H32664749", "&H00000000",
                0, 0, 0, 0,
                100, 100,
                0, 0, 0, outlineSize, 0, 7,
                marginH, marginH, marginV + (int)(fontsize * 2.4), 1
            ),
            new(
                "Character", "思源黑体 CN Bold", charaFontsize,
                "&H00FFFFFF", "&H000000FF", "&H32664749", "&H00000000",
                0, 0, 0, 0,
                100, 100,
                0, 0, 0, charaOutlineSize, 0, 7,
                0, 0, 0, 1
            ),
            new(
                "Screen", "思源黑体 CN Bold", charaFontsize,
                "&H00FFFFFF", "&H000000FF", "&H32664749", "&H00000000",
                0, 0, 0, 0,
                100, 100,
                0, 0, 0, outlineSize, 0, 7,
                0, 0, 0, 1
            )
        };

        return result;
    }

    private List<SubtitleEventItem> MakeDialogEvents(List<DialogFrameSet> dialogList)
    {
        var styles = MakeDialogStyles();
        var result = new List<SubtitleEventItem>();

        foreach (var dialogFrameSet in dialogList)
        {
            var characterName = dialogFrameSet.Dialog!.CharacterTranslated == "" ||
                                dialogFrameSet.Dialog.CharacterTranslated == dialogFrameSet.Dialog.CharacterOriginal
                ? dialogFrameSet.Dialog.CharacterTranslated
                : "";

            var originLine = dialogFrameSet.Dialog.BodyOriginal.Split("\n").Length;
            var styleName = "Line" + originLine;
            var style = styles.Find(s => s.Name == styleName)!;

            if (dialogFrameSet.IsJitter)
            {
                var constPosition = dialogFrameSet.Data[0].Point;
                var lastPosition = new Point(0, 0);
                var dialogEvents = new List<SubtitleEventItem>();
                var characterEvents = new List<SubtitleEventItem>();
                foreach (var frame in dialogFrameSet.Data)
                {
                    var x = style.MarginL;
                    var y = style.MarginV;
                    x += frame.Point.X - constPosition.X;
                    y += frame.Point.Y - constPosition.Y;
                    var body = MakeDialogTyper(dialogFrameSet.Dialog.BodyOriginal, new[] { 50, 80 }, frame.FrameIndex);

                    if (lastPosition.X == x && lastPosition.Y == y && body == dialogEvents[^1].Text)
                    {
                        dialogEvents[^1].End = frame.FrameEndTimeStr(_videoFrameRate);
                        characterEvents[^1].End = frame.FrameEndTimeStr(_videoFrameRate);
                    }
                    else
                    {
                        var dialogItem = SubtitleEventItem.Dialog(body,
                            frame.FrameTimeStr(_videoFrameRate),
                            frame.FrameEndTimeStr(_videoFrameRate), styleName);
                        dialogEvents.Add(dialogItem);

                        var characterItemPosition = frame.Point + new Size(frame.NameTagRect.Width + 10, 0);
                        var characterItemPositionTag =
                            $@"{{\pos({characterItemPosition.X},{characterItemPosition.Y})}}";

                        if (characterName == "") characterItemPositionTag = "";
                        var characterItem = SubtitleEventItem.Dialog(
                            characterItemPositionTag + characterName,
                            frame.FrameTimeStr(_videoFrameRate), frame.FrameEndTimeStr(_videoFrameRate),
                            "Character");
                        characterEvents.Add(characterItem);
                    }

                    lastPosition = new Point(x, y);
                }

                result.AddRange(dialogEvents);
                result.AddRange(characterEvents);
                result.Add(SubtitleEventItem.Comment("----------",
                    dialogFrameSet.Data[0].FrameTimeStr(_videoFrameRate),
                    dialogFrameSet.Data[^1].FrameEndTimeStr(_videoFrameRate), "Screen"
                ));
            }
            else
            {
                var startTime = dialogFrameSet.StartTime(_videoFrameRate);
                var endTime = dialogFrameSet.EndTime(_videoFrameRate);
                var body = MakeDialogTyper(dialogFrameSet.Dialog.BodyOriginal, new[] { 50, 80 });
                var dialogItem = SubtitleEventItem.Dialog(body, startTime, endTime, styleName);

                var characterItemPosition =
                    dialogFrameSet.Data[0].Point + new Size(dialogFrameSet.Data[0].NameTagRect.Width + 10, 0);
                var characterItemPositionTag = $@"{{\pos({characterItemPosition.X},{characterItemPosition.Y})}}";

                if (characterName == "") characterItemPositionTag = "";
                var characterItem = SubtitleEventItem.Dialog(
                    characterItemPositionTag + characterName,
                    startTime, endTime, "Character");

                result.Add(characterItem);
                result.Add(dialogItem);
                result.Add(SubtitleEventItem.Comment("----------",
                    dialogFrameSet.Data[0].FrameTimeStr(_videoFrameRate),
                    dialogFrameSet.Data[^1].FrameEndTimeStr(_videoFrameRate), "Screen"
                ));
            }
        }

        return result;
    }

    private static void ShowImg(Mat img, bool wait = false)
    {
        CvInvoke.Imshow("test", img);
        CvInvoke.WaitKey(wait ? 0 : 1);
    }

    public void Process()
    {
        var contentStartedAt = 740; // MatchContentStarted();
        Console.WriteLine($"Content started at Frame {contentStartedAt}");
        var dialogMatchResult = MatchDialogs(contentStartedAt);
        var dialogEvents = MakeDialogEvents(dialogMatchResult);
        // foreach (var item in dialogEvents)
        // {
        //     Console.WriteLine(item.ToString());
        // }

        var scriptInfo = new SubtitleScriptInfo(_videoResolution.Width, _videoResolution.Height,
            Path.GetFileNameWithoutExtension(_videoFilePath));
        var garbage = new SubtitleGarbage(_videoFilePath, _videoFilePath);

        var style = new SubtitleStyles(MakeDialogStyles().ToArray());
        var events = new SubtitleEvents(dialogEvents.ToArray());
        var assData = new Subtitle(scriptInfo, garbage, style, events);
        var outputFile = new StreamWriter(_outputFilePath, false, Encoding.UTF8);
        outputFile.Write(assData.ToString());
        outputFile.Close();
    }
}
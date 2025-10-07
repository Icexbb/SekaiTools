using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore.Process;

public enum TemplateUsage
{
    DialogNameTag,
    DialogContent,
    BannerContent,
    MarkerContent,
}

public partial class TemplateManager(Size videoResolution, bool noScale = false)
{
    private const string MenuSignBase = "menu-107px.png";
    private const string DbFontBase = "FOT-RodinNTLGPro-DB.otf";
    private const string EbFontBase = "FOT-RodinNTLGPro-EB.otf";

    private readonly Dictionary<TemplateUsage, Dictionary<string, Mat>?> _template = new();

    private Mat? _menuSign;

    private double VideoRatio => videoResolution.Width / (double)videoResolution.Height;


    public Mat GetMenuSign()
    {
        if (_menuSign != null) return _menuSign;
        var menuTemplatePath = ResourceManager.ResourcePath(MenuSignBase);
        if (!File.Exists(menuTemplatePath)) throw new FileNotFoundException();
        var menuTemplate = CvInvoke.Imread(menuTemplatePath, ImreadModes.Unchanged)!;
        int menuSize;
        if (videoResolution.Height / (double)videoResolution.Width > 16.0 / 9)
            menuSize = (int)(videoResolution.Height * 0.0741);
        else
            menuSize = (int)(videoResolution.Width * 0.0417);

        CvInvoke.Resize(menuTemplate, menuTemplate, new Size(menuSize, menuSize));
        // var result = new TemplateGrayAlpha(menuTemplate, false);
        _menuSign = menuTemplate;
        return menuTemplate;
    }

    private Size MaxSize(IEnumerable<Mat> mats, bool real = false)
    {
        var maxWidth = 0;
        var maxHeight = 0;
        foreach (var template in mats)
        {
            maxWidth = Math.Max(maxWidth, template.Width);
            maxHeight = Math.Max(maxHeight, template.Height);
        }

        return real ? new Size(maxWidth, maxHeight) :
            noScale ? new Size(maxWidth, maxHeight) : new Size(maxWidth / 5, maxHeight / 5);
    }

    public Size TemplateMaxSize(TemplateUsage usage, bool real = false)
    {
        var valueCollection = _template[usage]?.Values;
        return valueCollection != null ? MaxSize(valueCollection, real) : new Size(0, 0);
    }

    private int GetFontSize()
    {
        var scale = (noScale ? 1 : 5) * 0.95;
        var size = VideoRatio > 16 / 9.0
            ? videoResolution.Height * 0.043
            : videoResolution.Width * 0.024;
        var result = (int)(size * scale);
        return result;
    }

    private static Mat CropZero(Mat mat)
    {
        var temp = new Mat();
        CvInvoke.CvtColor(mat, temp, ColorConversion.Bgr2Gray);
        CvInvoke.Threshold(temp, temp, 1, 255, ThresholdType.Binary);
        var rect = CvInvoke.BoundingRectangle(temp);
        return new Mat(mat, rect);
    }

    private static Font GetFont(string fontFilePath, float fontSize)
    {
        var collection = new PrivateFontCollection();
        collection.AddFontFile(fontFilePath);
        var result = new Font(collection.Families[0], fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        return result;
    }

    private Font GetDbFont()
    {
        var fontFilePath = ResourceManager.ResourcePath(DbFontBase);
        return GetFont(fontFilePath, GetFontSize());
    }

    private Font GetEbFont()
    {
        var fontFilePath = ResourceManager.ResourcePath(EbFontBase);
        return GetFont(fontFilePath, GetFontSize());
    }

    private Mat CreateImageWithText(TemplateUsage usage, string text)
    {
        var font = usage switch
        {
            TemplateUsage.DialogNameTag => GetEbFont(),
            TemplateUsage.DialogContent or TemplateUsage.BannerContent or TemplateUsage.MarkerContent => GetDbFont(),
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };

        var byChar = false;
        // var byChar = font.Name.Contains("DB");
        // if (TextRegex().Matches(text).Count > 0)
        //     byChar = false;


        var bitmap = new Bitmap((int)(text.Length * font.Size * 2), (int)font.Size * 2);
        bitmap.MakeTransparent();
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        Action<GraphicsPath> drawStroke;
        Action<GraphicsPath> drawText;

        const int fillScale = 235;
        var fillColor = Color.FromArgb(255, fillScale, fillScale, fillScale);
        const int grayScale = 64;
        var strokeColor = Color.FromArgb(255, grayScale, grayScale, grayScale);
        switch (usage)
        {
            case TemplateUsage.BannerContent:
                drawStroke = path =>
                {
                    var width = font.Size / 4f;
                    using Pen pen = new(strokeColor, width);
                    pen.LineJoin = LineJoin.Round;
                    graphics.DrawPath(pen, path);
                };

                drawText = path =>
                {
                    using Brush brush = new SolidBrush(fillColor);
                    graphics.FillPath(brush, path);
                };
                if (byChar)
                {
                    for (var i = 0; i < text.Length; i++)
                    {
                        var sf = new StringFormat();
                        var pos = new Point((int)(10 + font.Size * 1.01 * i) + 1, 10);
                        using GraphicsPath path = new();
                        path.AddString(text[i].ToString(), font.FontFamily, (int)font.Style, font.Size, pos, sf);
                        // drawStroke(path);
                        drawText(path);
                    }
                }
                else
                {
                    using GraphicsPath path = new();

                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                        new StringFormat(StringFormatFlags.FitBlackBox)
                    );
                    // drawStroke(path);
                    drawText(path);
                }

                var mat = CropZero(bitmap.ToMat());

                var extendPixel = (int)(font.Size / 16f);
                var extendSize = new Size(mat.Width + extendPixel * 2, mat.Height + extendPixel * 2);
                var expandedMat = new Mat(extendSize, mat.Depth, mat.NumberOfChannels);
                const int bannerGrayScale = 100;
                mat.CopyTo(new Mat(expandedMat, new Rectangle(extendPixel, extendPixel, mat.Width, mat.Height)));

                var bgMat = new Mat(expandedMat.Size, expandedMat.Depth, expandedMat.NumberOfChannels);
                bgMat.SetTo(new MCvScalar(bannerGrayScale, bannerGrayScale, bannerGrayScale, 255));
                CvInvoke.BitwiseOr(bgMat, expandedMat, expandedMat);
                return expandedMat;
            default:
                drawStroke = path =>
                {
                    var width = font.Size / 5f;
                    using Pen pen = new(strokeColor, width);
                    pen.LineJoin = LineJoin.Round;
                    graphics.DrawPath(pen, path);
                };

                drawText = path =>
                {
                    using Brush brush = new SolidBrush(fillColor);
                    graphics.FillPath(brush, path);
                };
                if (byChar)
                {
                    for (var i = 0; i < text.Length; i++)
                    {
                        var sf = new StringFormat();
                        var pos = new Point((int)(10 + font.Size * 1.01 * i) + 1, 10);
                        using GraphicsPath path = new();
                        path.AddString(text[i].ToString(), font.FontFamily, (int)font.Style, font.Size, pos, sf);
                        drawStroke(path);
                        drawText(path);
                    }
                }
                else
                {
                    using GraphicsPath path = new();

                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                        new StringFormat());
                    drawStroke(path);
                    drawText(path);
                }

                return CropZero(bitmap.ToMat());
        }
    }


    public Mat GetTemplate(TemplateUsage usage, string text)
    {
        var usageDict = _template.GetValueOrDefault(usage);
        if (usageDict == null) _template[usage] = usageDict = new Dictionary<string, Mat>();

        if (usageDict.TryGetValue(text, out var template)) return template;

        var mat = CreateImageWithText(usage, text);
        usageDict[text] = mat;
        return mat;
    }

    [GeneratedRegex("[a-zA-Z0-9]")]
    private static partial Regex TextRegex();
}
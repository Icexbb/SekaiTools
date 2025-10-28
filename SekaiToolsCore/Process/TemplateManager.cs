using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore.Process;

public enum TemplateUsage
{
    DialogNameTag,
    DialogContent,
    BannerContent,
    MarkerContent
}

public class TemplateManager(Size videoResolution, bool noScale = false)
{
    private const string MenuSignBase = "menu-107px.png";
    private const string DbFontBase = "FOT-RodinNTLGPro-DB.otf";
    private const string EbFontBase = "FOT-RodinNTLGPro-EB.otf";

    private readonly Dictionary<TemplateUsage, Dictionary<string, Mat>?> _template = new();

    private Mat? _menuSign;

    public Mat GetMenuSign()
    {
        if (_menuSign != null) return _menuSign;
        var menuTemplatePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, MenuSignBase);
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

    public int GetFontSize()
    {
        var scale = noScale ? 1 : 5;
        var size = GetFontSize(videoResolution);
        var result = size * scale;
        return result;
    }

    public int GetFontSize(Size videoSize)
    {
        var scale = 0.95;
        var size = videoSize.Height / (double)videoSize.Height > 16 / 9.0
            ? videoSize.Height * 0.043
            : videoSize.Width * 0.024;
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
        var fontFilePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, DbFontBase);
        return GetFont(fontFilePath, GetFontSize());
    }

    private Font GetEbFont()
    {
        var fontFilePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, EbFontBase);
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

        var bitmap = new Bitmap((int)(text.Length * font.Size * 2), (int)font.Size * 2);
        bitmap.MakeTransparent();
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        Action<GraphicsPath> drawStroke;
        Action<GraphicsPath> drawText;


        switch (usage)
        {
            case TemplateUsage.BannerContent:
            {
                const int fillScale = 235;
                var fillColor = Color.FromArgb(255, fillScale, fillScale, fillScale);
                drawText = path =>
                {
                    using Brush brush = new SolidBrush(fillColor);
                    graphics.FillPath(brush, path);
                };

                using GraphicsPath path = new();

                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                    new StringFormat(StringFormatFlags.FitBlackBox)
                );
                drawText(path);
                var mat = CropZero(bitmap.ToMat());

                var extendPixel = (int)(font.Size / 16f);
                var extendSize = new Size(mat.Width + extendPixel * 2, mat.Height + extendPixel * 2);
                var expandedMat = new Mat(extendSize, mat.Depth, mat.NumberOfChannels);
                const int bannerGrayScale = 80;
                mat.CopyTo(new Mat(expandedMat, new Rectangle(extendPixel, extendPixel, mat.Width, mat.Height)));

                var bgMat = new Mat(expandedMat.Size, expandedMat.Depth, expandedMat.NumberOfChannels);
                bgMat.SetTo(new MCvScalar(bannerGrayScale, bannerGrayScale, bannerGrayScale, 255));
                CvInvoke.BitwiseOr(bgMat, expandedMat, expandedMat);
                return expandedMat;
            }
            case TemplateUsage.DialogNameTag:
            {
                const int fillScale = 235;
                var fillColor = Color.FromArgb(255, fillScale, fillScale, fillScale);
                const int grayScale = 64;
                var strokeColor = Color.FromArgb(255, grayScale, grayScale, grayScale);
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
                using GraphicsPath path = new();
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                    new StringFormat());
                drawStroke(path);
                drawText(path);
                return CropZero(bitmap.ToMat());
            }
            default:
            {
                const int fillScale = 235;
                var fillColor = Color.FromArgb(255, fillScale, fillScale, fillScale);
                const int grayScale = 64;
                var strokeColor = Color.FromArgb(255, grayScale, grayScale, grayScale);
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

                using GraphicsPath path = new();
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                    new StringFormat());
                drawStroke(path);
                drawText(path);
                return CropZero(bitmap.ToMat());
            }
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
}
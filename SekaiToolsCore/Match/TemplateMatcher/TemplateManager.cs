using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SkiaSharp;

namespace SekaiToolsCore.Match.TemplateMatcher;

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
    private SKTypeface? _dbTypeface;
    private SKTypeface? _ebTypeface;

    public Mat GetMenuSign()
    {
        if (_menuSign != null) return _menuSign;
        var menuTemplatePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, MenuSignBase);
        if (!File.Exists(menuTemplatePath)) throw new FileNotFoundException();
        var menuTemplate = CvInvoke.Imread(menuTemplatePath, ImreadModes.Unchanged)!;
        var menuSize = GetMenuSignSize(videoResolution);

        CvInvoke.Resize(menuTemplate, menuTemplate, new Size(menuSize, menuSize));
        _menuSign = menuTemplate;
        return menuTemplate;
    }

    public static int GetMenuSignSize(Size videoSize)
    {
        const double standardRatio = 16.0 / 9.0;
        var ratio = videoSize.Width / (double)videoSize.Height;
        var menuSize = ratio switch
        {
            < standardRatio => (int)(videoSize.Width * 0.0417),
            _ => (int)(videoSize.Height * 0.0741)
        };

        return menuSize;
    }

    public static int GetFontSize(Size videoSize, double scale = 0.95)
    {
        const double standardRatio = 16.0 / 9.0;
        var ratio = videoSize.Width / (double)videoSize.Height;
        var size = ratio switch
        {
            < standardRatio => (int)(videoSize.Width * 0.024),
            _ => (int)(videoSize.Height * 0.043)
        };
        var result = (int)(size * scale);
        return result;
    }

    public int GetFontSize(double fontScale = 0.95)
    {
        var size = GetFontSize(videoResolution, fontScale);
        var scale = noScale ? 1 : 5;
        var result = size * scale;
        return result;
    }

    private static Mat CropZero(Mat mat)
    {
        using var temp = new Mat();
        CvInvoke.CvtColor(mat, temp, ColorConversion.Bgr2Gray);
        CvInvoke.Threshold(temp, temp, 1, 255, ThresholdType.Binary);
        var rect = CvInvoke.BoundingRectangle(temp);
        return new Mat(mat, rect);
    }

    private static SKFont CreateFont(string fontFilePath, float fontSize)
    {
        var typeface = SKTypeface.FromFile(fontFilePath)
                       ?? throw new FileNotFoundException($"Font not found: {fontFilePath}");
        return new SKFont(typeface, fontSize);
    }

    private SKFont GetDbFont()
    {
        _dbTypeface ??= SKTypeface.FromFile(
            ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, DbFontBase));
        if (_dbTypeface == null) throw new FileNotFoundException($"Font not found: {DbFontBase}");
        return new SKFont(_dbTypeface, GetFontSize(0.95f));
    }

    private SKFont GetEbFont()
    {
        _ebTypeface ??= SKTypeface.FromFile(
            ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, EbFontBase));
        if (_ebTypeface == null) throw new FileNotFoundException($"Font not found: {EbFontBase}");
        return new SKFont(_ebTypeface, GetFontSize(0.95f));
    }

    private static Mat SkBitmapToMat(SKBitmap bitmap)
    {
        var mat = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 4);
        var pixels = bitmap.GetPixelSpan().ToArray();
        Marshal.Copy(pixels, 0, mat.DataPointer, pixels.Length);
        return mat;
    }

    private Mat CreateImageWithText(TemplateUsage usage, string text)
    {
        using var font = usage switch
        {
            TemplateUsage.DialogNameTag => GetEbFont(),
            TemplateUsage.DialogContent or TemplateUsage.BannerContent or TemplateUsage.MarkerContent => GetDbFont(),
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };
        font.Edging = SKFontEdging.SubpixelAntialias;

        var fontSize = font.Size;
        var width = (int)(text.Length * fontSize * 2);
        var height = (int)(fontSize * 2);

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var metrics = font.Metrics;
        float textX = 10f;
        float textY = 10f - metrics.Ascent;

        const int fillGray = 255;
        var fillColor = new SKColor(fillGray, fillGray, fillGray);
        using var fillPaint = new SKPaint();
        fillPaint.Color = fillColor;
        fillPaint.IsAntialias = true;
        fillPaint.Style = SKPaintStyle.Fill;

        const int strokeGray = 80;

        using var strokePaint = new SKPaint();
        strokePaint.Color = new SKColor(strokeGray, strokeGray, strokeGray);
        strokePaint.IsAntialias = true;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = fontSize / 5f;
        strokePaint.StrokeJoin = SKStrokeJoin.Round;

        switch (usage)
        {
            case TemplateUsage.BannerContent:
                break;
            case TemplateUsage.DialogNameTag:
            case TemplateUsage.DialogContent:
            case TemplateUsage.MarkerContent:
                canvas.DrawText(text, textX, textY, font, strokePaint);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(usage), usage, null);
        }

        canvas.DrawText(text, textX, textY, font, fillPaint);

        canvas.Flush();

        using var mat = SkBitmapToMat(bitmap);
        var cropped = CropZero(mat);

        if (usage != TemplateUsage.BannerContent) return cropped;

        var extendPixel = (int)(fontSize / 16f);
        var extendSize = new Size(cropped.Width + extendPixel * 2, cropped.Height + extendPixel * 2);
        var expandedMat = new Mat(extendSize, cropped.Depth, cropped.NumberOfChannels);
        const int bannerGrayScale = 80;
        cropped.CopyTo(new Mat(expandedMat, new Rectangle(extendPixel, extendPixel, cropped.Width, cropped.Height)));

        using var bgMat = new Mat(expandedMat.Size, expandedMat.Depth, expandedMat.NumberOfChannels);
        bgMat.SetTo(new MCvScalar(bannerGrayScale, bannerGrayScale, bannerGrayScale, 255));
        CvInvoke.BitwiseOr(bgMat, expandedMat, expandedMat);
        cropped.Dispose();
        return expandedMat;
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
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace Core;

[SuppressMessage("Interoperability", "CA1416")]
public class TemplateManager
{
    private readonly Size _videoResolution;
    private double VideoRatio => _videoResolution.Width / (double)_videoResolution.Height;
    private readonly Dictionary<string, Mat> _dbTemplate = new();
    private readonly Dictionary<string, Mat> _ebTemplate = new();
    private readonly string[] _dbTexts;
    private readonly string[] _ebTexts;
    private readonly bool _noScale;

    public Size EbTemplateMaxSize()
    {
        var maxWidth = 0;
        var maxHeight = 0;
        foreach (var template in _ebTemplate.Values)
        {
            maxWidth = Math.Max(maxWidth, template.Width);
            maxHeight = Math.Max(maxHeight, template.Height);
        }

        return _noScale ? new Size(maxWidth, maxHeight) : new Size(maxWidth / 5, maxHeight / 5);
    }

    public Size DbTemplateMaxSize()
    {
        var maxWidth = 0;
        var maxHeight = 0;
        foreach (var template in _dbTemplate.Values)
        {
            maxWidth = Math.Max(maxWidth, template.Width);
            maxHeight = Math.Max(maxHeight, template.Height);
        }

        return _noScale ? new Size(maxWidth, maxHeight) : new Size(maxWidth / 5, maxHeight / 5);
    }

    public TemplateManager(Size videoResolution, string[] dbTexts, string[] ebTexts, bool noScale = false)
    {
        _videoResolution = videoResolution;
        _dbTexts = dbTexts;
        _ebTexts = ebTexts;
        _noScale = noScale;
        GenerateTemplates();
    }

    private int GetFontSize()
    {
        var scale = (_noScale ? 1 : 5) * 0.94;
        var size = VideoRatio > 16 / 9.0
            ? _videoResolution.Height * 0.043
            : _videoResolution.Width * 0.024;
        var result = (int)(size * scale);
        Console.WriteLine(result);
        return result;
    }

    private static Mat CropNoneZero(Mat mat)
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
        var fontFilePath = Path.Join(Directory.GetCurrentDirectory(), "fonts", "FOT-RodinNTLGPro-DB.otf");
        return GetFont(fontFilePath, GetFontSize());
    }

    private Font GetEbFont()
    {
        var fontFilePath = Path.Join(Directory.GetCurrentDirectory(), "fonts", "FOT-RodinNTLGPro-EB.otf");
        return GetFont(fontFilePath, GetFontSize());
    }

    private static Mat CreateImageWithText(Font font, string text)
    {
        var bitmap = new Bitmap((int)(text.Length * font.Size * 2), (int)font.Size * 2);
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = new())
        {
            path.AddString(
                text, font.FontFamily, (int)font.Style, font.Size, new Point(0, 0),
                new StringFormat());

            // 描边
            using (Pen pen = new(Color.Gray, font.Size / 4f))
            {
                pen.LineJoin = LineJoin.Round;
                graphics.DrawPath(pen, path);
            }

            // 填充
            using (Brush brush = new SolidBrush(Color.White))
            {
                graphics.FillPath(brush, path);
            }
        }

        return CropNoneZero(bitmap.ToMat());
    }

    public Mat GetDbTemplate(string text)
    {
        if (_dbTemplate.TryGetValue(text, out var template)) return template;

        var font = GetDbFont();
        var mat = CreateImageWithText(font, text);
        _dbTemplate[text] = mat;
        return mat;
    }

    public Mat GetEbTemplate(string text)
    {
        if (_ebTemplate.TryGetValue(text, out var template)) return template;

        var font = GetEbFont();
        var mat = CreateImageWithText(font, text);
        _ebTemplate[text] = mat;
        return mat;
    }

    private void GenerateDbTemplates()
    {
        foreach (var text in _dbTexts) GetDbTemplate(text);
    }

    private void GenerateEbTemplates()
    {
        foreach (var text in _ebTexts) GetEbTemplate(text);
    }

    private void GenerateTemplates()
    {
        GenerateDbTemplates();
        GenerateEbTemplates();
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore;

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
            _noScale ? new Size(maxWidth, maxHeight) : new Size(maxWidth / 5, maxHeight / 5);
    }

    public Size EbTemplateMaxSize(bool real = false) => MaxSize(_ebTemplate.Values, real);

    public Size DbTemplateMaxSize(bool real = false) => MaxSize(_dbTemplate.Values, real);

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
        var scale = (_noScale ? 1 : 5) * 0.95;
        var size = VideoRatio > 16 / 9.0
            ? _videoResolution.Height * 0.043
            : _videoResolution.Width * 0.024;
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
        var byChar = font.Name.Contains("DB");
        if (Regex.Matches(text, "[a-zA-Z0-9]").Count > 0)
            byChar = false;


        var bitmap = new Bitmap((int)(text.Length * font.Size * 2), (int)font.Size * 2);
        bitmap.MakeTransparent();
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        using (GraphicsPath path = new())
        {
            if (byChar)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    var sf = new StringFormat();
                    var pos = new Point((int)(10 + font.Size * 1.01 * i), 10);
                    path.AddString(text[i].ToString(), font.FontFamily, (int)font.Style, font.Size, pos, sf);
                }
            }
            else
            {
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                    new StringFormat());
            }

            using Pen pen = new(Color.FromArgb(64, 128, 128, 128), font.Size / 4.5f);
            pen.LineJoin = LineJoin.Round;
            graphics.DrawPath(pen, path);
            // 填充
            using Brush brush = new SolidBrush(Color.White);
            graphics.FillPath(brush, path);
        }

        return CropZero(bitmap.ToMat());
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
        if (text.Contains('・')) text = text[..text.IndexOf('・')];

        if (_ebTemplate.TryGetValue(text, out var template)) return template;

        var font = GetEbFont();
        var mat = CreateImageWithText(font, text);
        _ebTemplate[text] = mat;
        return mat;
    }

    private void GenerateDbTemplates()
    {
        Parallel.ForEach(_dbTexts, s => GetDbTemplate(s));
    }

    private void GenerateEbTemplates()
    {
        Parallel.ForEach(_ebTexts, s => GetEbTemplate(s));
    }

    private void GenerateTemplates()
    {
        // GenerateDbTemplates();
        // GenerateEbTemplates();
        Parallel.Invoke([GenerateEbTemplates, GenerateDbTemplates]);

        // var dbSize = DbTemplateMaxSize(real: true);
        //
        // foreach (var t in _dbTexts)
        // {
        //     var text = _dbTemplate[t];
        //     var bg = new Mat(dbSize, text.Depth, text.NumberOfChannels);
        //     bg.SetTo(new MCvScalar(0));
        //     var roi = new Mat(bg, new Rectangle(0, 0, text.Width, text.Height));
        //     text.CopyTo(roi);
        //     _dbTemplate[t] = bg;
        // }
    }
}
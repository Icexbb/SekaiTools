using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

[SuppressMessage("Interoperability", "CA1416")]
public class TemplateManager
{
    private readonly Size _videoResolution;
    private double VideoRatio => _videoResolution.Width / (double)_videoResolution.Height;
    private readonly Dictionary<string, Mat> _dbTemplate = new();
    private readonly Dictionary<string, Mat> _ebTemplate = new();
    private Mat? _menuSign;
    private readonly string[] _dbTexts;
    private readonly string[] _ebTexts;
    private readonly bool _noScale;


    private static string ResourcePath(string fileName)
    {
        string[] baseDirs =
        [
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            AppDomain.CurrentDomain.BaseDirectory,
            AppDomain.CurrentDomain.RelativeSearchPath ?? string.Empty,
            AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty
        ];
        foreach (var baseDir in baseDirs)
        {
            var filename = Path.Combine(baseDir, "Resource", fileName);
            if (File.Exists(filename)) return filename;
        }

        throw new FileNotFoundException($"{Path.Combine("Resource", fileName)} not found");
    }

    public Mat GetMenuSign()
    {
        if (_menuSign != null) return _menuSign;
        var menuTemplatePath = ResourcePath("menu-107px.png");
        if (!File.Exists(menuTemplatePath)) throw new FileNotFoundException();
        var menuTemplate = CvInvoke.Imread(menuTemplatePath, ImreadModes.Unchanged)!;
        int menuSize;
        if (_videoResolution.Height / (double)_videoResolution.Width > 16.0 / 9)
            menuSize = (int)(_videoResolution.Height * 0.0741);
        else
            menuSize = (int)(_videoResolution.Width * 0.0417);

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
            _noScale ? new Size(maxWidth, maxHeight) : new Size(maxWidth / 5, maxHeight / 5);
    }

    public Size EbTemplateMaxSize(bool real = false) => MaxSize(_ebTemplate.Values, real);

    public Size DbTemplateMaxSize(bool real = false) => MaxSize(_dbTemplate.Values, real);

    public TemplateManager(Size videoResolution, IEnumerable<string> dbTexts, IEnumerable<string> ebTexts,
        bool noScale = false)
    {
        _videoResolution = videoResolution;
        _dbTexts = dbTexts.GroupBy(p => p).Select(p => p.Key).ToArray();
        _ebTexts = ebTexts.GroupBy(p => p).Select(p => p.Key).ToArray();
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
        var fontFilePath = ResourcePath("FOT-RodinNTLGPro-DB.otf");
        return GetFont(fontFilePath, GetFontSize());
    }

    private Font GetEbFont()
    {
        var fontFilePath = ResourcePath("FOT-RodinNTLGPro-EB.otf");
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
        if (byChar)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var sf = new StringFormat();
                var pos = new Point((int)(10 + font.Size * 1.01 * i), 10);
                using GraphicsPath path = new();
                path.AddString(text[i].ToString(), font.FontFamily, (int)font.Style, font.Size, pos, sf);
                using Pen pen = new(Color.FromArgb(255, 64, 64, 64), font.Size / 4.5f);
                pen.LineJoin = LineJoin.Round;
                graphics.DrawPath(pen, path);
                // 填充
                using Brush brush = new SolidBrush(Color.White);
                graphics.FillPath(brush, path);
            }
        }
        else
        {
            using GraphicsPath path = new();

            path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new Point(10, 10),
                new StringFormat());
            using Pen pen = new(Color.FromArgb(255, 64, 64, 64), font.Size / 4.5f);
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
        if (_ebTemplate.TryGetValue(text, out var template)) return template;

        var font = GetEbFont();
        var mat = CreateImageWithText(font, text);
        _ebTemplate[text] = mat;
        return mat;
    }

    private void GenerateDbTemplates()
    {
        foreach (var s in _dbTexts) GetDbTemplate(s);
    }


    private void GenerateEbTemplates()
    {
        foreach (var s in _ebTexts) GetEbTemplate(s);
    }

    private void GenerateTemplates()
    {
        GenerateDbTemplates();
        GenerateEbTemplates();
    }
}
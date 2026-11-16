using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Timing;
using Xunit;

namespace SekaiTools.Tests;

public class VideoTimingVisualEvidenceTests
{
    [Fact]
    public void NameEvidenceRejectsClippedBackgroundAndKeepsInteriorGlyphs()
    {
        using var image = new Mat(new Size(128, 48), DepthType.Cv8U, 3);
        image.SetTo(new MCvScalar(0));
        using (var background = new Mat(image, new Rectangle(0, 0, 5, 48)))
            background.SetTo(new MCvScalar(255, 255, 255));
        using (var glyph = new Mat(image, new Rectangle(15, 15, 8, 18)))
            glyph.SetTo(new MCvScalar(255, 255, 255));
        using var mask = VideoTimingRecognizer.TextMask(image, rejectBorderComponents: true);
        Assert.Equal(144, CvInvoke.CountNonZero(mask));
    }

    [Fact]
    public void ShiftedGlyphsAreRetainedButAReplacementIsNot()
    {
        using var previous = Mask();
        using var shifted = Mask();
        using var replacement = Mask();
        CvInvoke.Rectangle(previous, new Rectangle(15, 15, 8, 18), new MCvScalar(255), -1);
        CvInvoke.Rectangle(previous, new Rectangle(35, 18, 6, 12), new MCvScalar(255), -1);
        CvInvoke.Rectangle(shifted, new Rectangle(19, 22, 8, 18), new MCvScalar(255), -1);
        CvInvoke.Rectangle(shifted, new Rectangle(39, 25, 6, 12), new MCvScalar(255), -1);
        CvInvoke.Rectangle(replacement, new Rectangle(85, 15, 8, 18), new MCvScalar(255), -1);
        Assert.True(VideoTimingRecognizer.Retained(previous, shifted) > .98);
        Assert.True(VideoTimingRecognizer.Retained(previous, replacement) < .1);

        static Mat Mask()
        {
            var mask = new Mat(new Size(128, 64), DepthType.Cv8U, 1);
            mask.SetTo(new MCvScalar(0));
            return mask;
        }
    }

    [Fact]
    public void InitialThinDashIsBodyEvidenceButNotNameOrBackgroundEvidence()
    {
        using var image = new Mat(new Size(128, 80), DepthType.Cv8U, 3);
        image.SetTo(new MCvScalar(0));
        using (var dash = new Mat(image, new Rectangle(15, 25, 32, 2)))
            dash.SetTo(new MCvScalar(255, 255, 255));
        using var name = VideoTimingRecognizer.TextMask(image);
        using var body = VideoTimingRecognizer.TextMask(image, includeInitialPunctuation: true);
        Assert.Equal(0, CvInvoke.CountNonZero(name));
        Assert.Equal(64, CvInvoke.CountNonZero(body));

        image.SetTo(new MCvScalar(0));
        using (var dash = new Mat(image, new Rectangle(15, 60, 32, 2)))
            dash.SetTo(new MCvScalar(255, 255, 255));
        using var background = VideoTimingRecognizer.TextMask(image, includeInitialPunctuation: true);
        Assert.Equal(0, CvInvoke.CountNonZero(background));
    }
}

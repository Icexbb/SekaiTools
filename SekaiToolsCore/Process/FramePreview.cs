using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

internal static class FramePreview
{
    private const int MaximumWidth = 960;
    private const int MaximumHeight = 540;

    // This image is only for display. Recognition continues to use the full frame.
    internal static Mat Create(Mat source)
    {
        var scale = Math.Min(1d, Math.Min(MaximumWidth / (double)source.Width,
            MaximumHeight / (double)source.Height));
        if (scale >= 1) return source.Clone();
        var preview = new Mat();
        try
        {
            CvInvoke.Resize(source, preview, new Size(Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale))), interpolation: Inter.Area);
            return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }
    }
}

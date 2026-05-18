using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process.Model;

public class GaMat : IDisposable // Gray and Alpha Mat
{
    public readonly Mat Alpha;
    public readonly Mat Gray;

    public GaMat(IInputArray src, bool resize = true)
    {
        var grayImage = new Mat();
        var alphaChannel = new Mat();
        try
        {
            CvInvoke.CvtColor(src, grayImage, ColorConversion.Bgra2Gray);
            CvInvoke.ExtractChannel(src, alphaChannel, 3);
            if (resize)
            {
                const int scaleRatio = 5;
                var size = new Size(grayImage.Size.Width / scaleRatio, grayImage.Size.Height / scaleRatio);
                CvInvoke.Resize(grayImage, grayImage, size);
                CvInvoke.Resize(alphaChannel, alphaChannel, size);
            }
        }
        catch
        {
            grayImage.Dispose();
            alphaChannel.Dispose();
            throw;
        }

        Gray = grayImage;
        Alpha = alphaChannel;
    }

    public Size Size => Gray.Size;

    public void Dispose()
    {
        Gray.Dispose();
        Alpha.Dispose();
    }
}
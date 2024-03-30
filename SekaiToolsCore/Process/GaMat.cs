using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process;

public class GaMat // Gray and Alpha Mat
{
    public readonly Mat Gray;
    public readonly Mat Alpha;
    public Size Size => Gray.Size;

    public GaMat(IInputArray src, bool resize = true)
    {
        var grayImage = new Mat();
        var alphaChannel = new Mat();
        CvInvoke.CvtColor(src, grayImage, ColorConversion.Bgra2Gray);
        CvInvoke.ExtractChannel(src, alphaChannel, 3);
        if (resize)
        {
            const int scaleRatio = 5;
            var size = new Size(grayImage.Size.Width / scaleRatio, grayImage.Size.Height / scaleRatio);
            CvInvoke.Resize(grayImage, grayImage, size);
            CvInvoke.Resize(alphaChannel, alphaChannel, size);
        }

        Gray = grayImage;
        Alpha = alphaChannel;
    }
}
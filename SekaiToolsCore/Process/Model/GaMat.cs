using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process.Model;

public class GaMat : IDisposable // Gray and Alpha Mat
{
    private readonly Dictionary<int, GaMatLayer> _scaledLayers = new();
    private readonly object _scaledLayersLock = new();
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

    public GaMatLayer GetScaledLayer(int divisor)
    {
        if (divisor <= 1) return new GaMatLayer(Gray, Alpha);

        lock (_scaledLayersLock)
        {
            if (_scaledLayers.TryGetValue(divisor, out var cached))
                return cached;

            var size = new Size(
                Math.Max(1, Gray.Width / divisor),
                Math.Max(1, Gray.Height / divisor));
            var gray = new Mat();
            var alpha = new Mat();
            try
            {
                CvInvoke.Resize(Gray, gray, size, interpolation: Inter.Linear);
                CvInvoke.Resize(Alpha, alpha, size, interpolation: Inter.Nearest);
                var layer = new GaMatLayer(gray, alpha);
                _scaledLayers.Add(divisor, layer);
                return layer;
            }
            catch
            {
                gray.Dispose();
                alpha.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_scaledLayersLock)
        {
            foreach (var layer in _scaledLayers.Values)
            {
                layer.Gray.Dispose();
                layer.Alpha.Dispose();
            }
            _scaledLayers.Clear();
        }
        Gray.Dispose();
        Alpha.Dispose();
    }
}

public readonly record struct GaMatLayer(Mat Gray, Mat Alpha)
{
    public Size Size => Gray.Size;
}

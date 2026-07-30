using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Process.Model;

public class GaMat : IDisposable // Gray and Alpha Mat
{
    private readonly Dictionary<GaMatScale, GaMatLayer> _scaledLayers = new();
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
        return GetScaledLayer(1, divisor);
    }

    public GaMatLayer GetScaledLayer(double scale, int divisor)
    {
        if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
        if (Math.Abs(scale - 1) < double.Epsilon && divisor == 1)
            return new GaMatLayer(Gray, Alpha);

        var key = new GaMatScale(scale, divisor);

        lock (_scaledLayersLock)
        {
            if (_scaledLayers.TryGetValue(key, out var cached))
                return cached;

            var size = new Size(
                Math.Max(1, (int)Math.Round(Gray.Width * scale / divisor)),
                Math.Max(1, (int)Math.Round(Gray.Height * scale / divisor)));
            var gray = new Mat();
            var alpha = new Mat();
            try
            {
                CvInvoke.Resize(Gray, gray, size, interpolation: Inter.Linear);
                CvInvoke.Resize(Alpha, alpha, size, interpolation: Inter.Nearest);
                var layer = new GaMatLayer(gray, alpha);
                _scaledLayers.Add(key, layer);
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

internal readonly record struct GaMatScale(double Scale, int Divisor);

public readonly record struct GaMatLayer(Mat Gray, Mat Alpha)
{
    public Size Size => Gray.Size;
}

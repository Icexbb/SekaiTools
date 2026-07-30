using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsCore.Match.TemplateMatcher;

/// <summary>
///     保存单帧共享的灰度图和按 ROI 延迟生成的缩放层。
/// </summary>
public sealed class FrameMatchContext : IDisposable
{
    private readonly Dictionary<ScaledRegion, Mat> _scaledGrayRegions = new();

    public FrameMatchContext()
    {
        Gray = new Mat();
    }

    public Mat Gray { get; }
    public Size Size => Gray.Size;

    public void Update(Mat source)
    {
        ClearScaledRegions();
        switch (source.NumberOfChannels)
        {
            case 1:
                source.CopyTo(Gray);
                break;
            case 3:
                CvInvoke.CvtColor(source, Gray, ColorConversion.Bgr2Gray);
                break;
            case 4:
                CvInvoke.CvtColor(source, Gray, ColorConversion.Bgra2Gray);
                break;
            default:
                throw new InvalidDataException($"不支持的帧通道数: {source.NumberOfChannels}");
        }
    }

    public Mat CreateGrayRoi(Rectangle region)
    {
        return new Mat(Gray, region);
    }

    public Mat GetScaledGrayRoi(Rectangle region, int divisor, Inter interpolation)
    {
        if (divisor <= 1) throw new ArgumentOutOfRangeException(nameof(divisor));

        var key = new ScaledRegion(region, divisor, interpolation);
        if (_scaledGrayRegions.TryGetValue(key, out var cached))
            return cached;

        using var source = CreateGrayRoi(region);
        var width = Math.Max(1, region.Width / divisor);
        var height = Math.Max(1, region.Height / divisor);
        var scaled = new Mat();
        CvInvoke.Resize(source, scaled, new Size(width, height), interpolation: interpolation);
        _scaledGrayRegions.Add(key, scaled);
        return scaled;
    }

    public void Dispose()
    {
        ClearScaledRegions();
        Gray.Dispose();
    }

    private void ClearScaledRegions()
    {
        foreach (var region in _scaledGrayRegions.Values)
            region.Dispose();
        _scaledGrayRegions.Clear();
    }

    private readonly record struct ScaledRegion(Rectangle Region, int Divisor, Inter Interpolation);
}

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
    private readonly HashSet<ScaledRegion> _validRegions = [];
    private readonly Mat _coarseResult = new();
    private readonly Mat _refinementResult = new();
    private long _retainedRegionBytes;

    public FrameMatchContext()
    {
        Gray = new Mat();
    }

    public Mat Gray { get; }
    public Size Size => Gray.Size;

    public void Dispose()
    {
        ClearScaledRegions();
        Gray.Dispose();
        _coarseResult.Dispose();
        _refinementResult.Dispose();
    }

    public void Update(Mat source)
    {
        if (_retainedRegionBytes > 16L * 1024 * 1024) ClearScaledRegions();
        _validRegions.Clear();
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
        if (_validRegions.Contains(key) && _scaledGrayRegions.TryGetValue(key, out var cached))
            return cached;

        using var source = CreateGrayRoi(region);
        var width = Math.Max(1, region.Width / divisor);
        var height = Math.Max(1, region.Height / divisor);
        var bytes = (long)width * height;
        if (!_scaledGrayRegions.TryGetValue(key, out var scaled))
        {
            if (_scaledGrayRegions.Count >= 16 || _retainedRegionBytes + bytes > 16L * 1024 * 1024)
                ClearScaledRegions();
            scaled = new Mat();
            _scaledGrayRegions.Add(key, scaled);
            _retainedRegionBytes += bytes;
        }
        CvInvoke.Resize(source, scaled, new Size(width, height), interpolation: interpolation);
        _validRegions.Add(key);
        return scaled;
    }

    internal MatchResultLease RentMatchResult(Size image, Size template, bool refinement = false)
    {
        var bytes = Math.Max(0L, (long)image.Width - template.Width + 1)
                    * Math.Max(0L, (long)image.Height - template.Height + 1) * sizeof(float);
        var retain = bytes <= 8L * 1024 * 1024;
        return new MatchResultLease(retain ? (refinement ? _refinementResult : _coarseResult) : new Mat(), !retain);
    }

    internal readonly struct MatchResultLease(Mat image, bool owned) : IDisposable
    {
        internal Mat Image { get; } = image;
        public void Dispose() { if (owned) Image.Dispose(); }
    }

    private void ClearScaledRegions()
    {
        foreach (var region in _scaledGrayRegions.Values)
            region.Dispose();
        _scaledGrayRegions.Clear();
        _validRegions.Clear();
        _retainedRegionBytes = 0;
    }

    private readonly record struct ScaledRegion(Rectangle Region, int Divisor, Inter Interpolation);
}

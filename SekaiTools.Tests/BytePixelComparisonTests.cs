using System.Drawing;
using System.Runtime.InteropServices;
using DecoderProbe;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiTools.Tests;

public class BytePixelComparisonTests
{
    [Fact]
    public void NonContiguousRoiExcludesPaddingAndCountsKnownDifferences()
    {
        using var a = new Mat(4, 6, DepthType.Cv8U, 3);
        using var b = new Mat(4, 6, DepthType.Cv8U, 3);
        a.SetTo(new MCvScalar(50, 50, 50));
        b.SetTo(new MCvScalar(200, 200, 200));
        using var left = new Mat(a, new Rectangle(1, 1, 3, 2));
        using var right = new Mat(b, new Rectangle(1, 1, 3, 2));
        left.SetTo(new MCvScalar()); right.SetTo(new MCvScalar());
        Assert.False(right.IsContinuous);
        Marshal.WriteByte(right.DataPointer, 0, 1);
        Marshal.WriteByte(right.DataPointer, right.Step + 8, 255);
        Assert.Equal(new BytePixelDifference(255, 2), BytePixelComparison.Compare(left, right));
    }

    [Fact]
    public void EqualPixelsReturnZeroDespiteDifferentSurroundingStorage()
    {
        using var a = new Mat(4, 6, DepthType.Cv8U, 3);
        using var b = new Mat(4, 6, DepthType.Cv8U, 3);
        a.SetTo(new MCvScalar(50, 50, 50)); b.SetTo(new MCvScalar(200, 200, 200));
        using var left = new Mat(a, new Rectangle(1, 1, 3, 2));
        using var right = new Mat(b, new Rectangle(1, 1, 3, 2));
        left.SetTo(new MCvScalar(23, 42, 66)); right.SetTo(new MCvScalar(23, 42, 66));
        Assert.Equal(new BytePixelDifference(0, 0), BytePixelComparison.Compare(left, right));
    }

    [Fact]
    public void DifferentShapeIsRejected()
    {
        using var a = new Mat(2, 3, DepthType.Cv8U, 3);
        using var b = new Mat(2, 3, DepthType.Cv8U, 1);
        Assert.Throws<ArgumentException>(() => BytePixelComparison.Compare(a, b));
    }
}

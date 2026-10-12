using Emgu.CV;
using Emgu.CV.CvEnum;

namespace DecoderProbe;

internal readonly record struct BytePixelDifference(int Maximum, int UnequalBytes);

internal static class BytePixelComparison
{
    public static BytePixelDifference Compare(Mat left, Mat right)
    {
        if (left.IsEmpty || right.IsEmpty || left.Depth != DepthType.Cv8U || right.Depth != DepthType.Cv8U ||
            left.Size != right.Size || left.NumberOfChannels != right.NumberOfChannels)
            throw new ArgumentException("Byte comparison requires equal nonempty 8-bit image shapes");
        var a = (byte[])left.GetData(false);
        var b = (byte[])right.GetData(false);
        if (a.Length != b.Length) throw new InvalidDataException("Different pixel byte counts");
        var maximum = 0;
        var unequal = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var difference = Math.Abs((int)a[i] - b[i]);
            maximum = Math.Max(maximum, difference);
            if (difference != 0) unequal++;
        }
        return new BytePixelDifference(maximum, unequal);
    }
}

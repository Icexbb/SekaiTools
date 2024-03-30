using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore;

public static class Utils
{
    public static bool IsSorted<T>(this IEnumerable<T> enumerable, bool strictIncreasing = true) where T : IComparable
    {
        var comparer = Comparer<T>.Default;
        T previous = default!;
        var first = true;
        var sign = strictIncreasing ? 1 : 0;
        foreach (var item in enumerable)
        {
            if (!first)
            {
                if (sign == 0) sign = int.Sign(comparer.Compare(previous, item));
                else if (sign != int.Sign(comparer.Compare(previous, item)))
                    return false;
            }

            first = false;
            previous = item;
        }

        return true;
    }

    public static T Middle<T>(T a, T b, T c) where T : IComparable
    {
        if (a.CompareTo(b) < 0)
        {
            if (b.CompareTo(c) < 0)
                return b;
            else if (a.CompareTo(c) < 0)
                return c;
            else
                return a;
        }


        if (a.CompareTo(c) < 0)
            return a;
        if (b.CompareTo(c) < 0)
            return c;
        return b;
    }

    public static void Extend(this Rectangle rect, int x, int y)
    {
        rect.X -= x;
        rect.Y -= y;
        rect.Width += x * 2;
        rect.Height += y * 2;
    }

    public static void Extend(this Rectangle rect, double ratio)
    {
        var x = (int)(rect.Width * ratio);
        var y = (int)(rect.Height * ratio);
        rect.Extend(x, y);
    }

    public static Rectangle FromCenter(Point center, Size size)
    {
        return new Rectangle(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);
    }

    public static Point Center(this Rectangle rect)
    {
        return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    public static Point Center(this Size size)
    {
        return new Point(size.Width / 2, size.Height / 2);
    }

    public static Point Resize(this Point point, double ratio)
    {
        return new Point((int)(point.X * ratio), (int)(point.Y * ratio));
    }

    public static Size Resize(this Size size, double ratio)
    {
        return new Size((int)(size.Width * ratio), (int)(size.Height * ratio));
    }

    public static void MatRemoveErrorInf(this Mat mat)
    {
        Mat positiveInf = new(mat.Size, mat.Depth, 1);
        Mat negativeInf = new(mat.Size, mat.Depth, 1);

        positiveInf.SetTo(new MCvScalar(1));
        negativeInf.SetTo(new MCvScalar(0));

        var mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, positiveInf, mask, CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);

        mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, negativeInf, mask, CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);
    }
}
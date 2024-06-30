using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore;

public static partial class Utils
{
    public static int LineCount(this string str)
        => str.Split('\n').Select(value => value.Length > 0 ? 1 : 0).Sum();

    public static int Count(this string str, string part)
    {
        int count = 0;
        int i = 0;
        while ((i = str.IndexOf(part, i)) != -1)
        {
            i += part.Length;
            count++;
        }
        return count;
    }

    public static string Remains(this TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalSeconds >= 1)
            return $"{timeSpan.Seconds}s";
        return $"{timeSpan.Milliseconds}ms";
    }


    public static int MaxLineLength(this string str)
        => str.Split('\n').Max(x => x.Trim().Length);


    public static IEnumerable<T> Contact<T>(params IEnumerable<T>[] arrays)
    {
        return arrays.SelectMany(x => x);
    }

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
        Mat nan = new(mat.Size, mat.Depth, 1);

        positiveInf.SetTo(new MCvScalar(1));
        negativeInf.SetTo(new MCvScalar(0));
        nan.SetTo(new MCvScalar(float.NaN));


        var mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, positiveInf, mask, CmpType.GreaterEqual);
        mat.SetTo(new MCvScalar(0), mask);

        mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, negativeInf, mask, CmpType.LessEqual);
        mat.SetTo(new MCvScalar(0), mask);

        mask = new Mat(mat.Size, mat.Depth, 1);
        CvInvoke.Compare(mat, nan, mask, CmpType.Equal);
        mat.SetTo(new MCvScalar(0), mask);
    }
}

public partial class Utils
{
    public static class TimerHelper
    {
        public static Timer SetTimeout(Action fn, long interval)
        {
            Timer? timer1 = null;
            var callback = new TimerCallback(_ =>
            {
                if (timer1 == null) fn.Invoke();
                else timer1.Dispose();
            });
            timer1 = new Timer(callback, null, interval, -1);
            return timer1;
        }

        public static Timer SetInterval(Action fn, long interval, ulong times = 0)
        {
            Timer? timer1 = null;
            var times2 = times;

            var callback = times > 0
                ? new TimerCallback(_ =>
                {
                    if (--times == 0)
                    {
                        try
                        {
                            timer1!.Dispose();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                    else if (times2 <= times)
                    {
                        try
                        {
                            timer1!.Dispose();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }

                        return;
                    }

                    if (times < times2)
                    {
                        times2 = times;
                    }

                    fn.Invoke();
                })
                : new TimerCallback(_ => { fn.Invoke(); });

            timer1 = new Timer(callback, null, interval, interval);

            return timer1;
        }
    }
}
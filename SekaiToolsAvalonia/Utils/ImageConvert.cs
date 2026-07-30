using Avalonia.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Util;

namespace SekaiToolsAvalonia.Utils;

public static class ImageConvert
{
    public static Bitmap? MatToBitmap(Mat mat)
    {
        if (mat.IsEmpty) return null;

        using var buf = new VectorOfByte();
        CvInvoke.Imencode(".jpg", mat, buf);
        using var ms = new MemoryStream(buf.ToArray());
        return new Bitmap(ms);
    }
}
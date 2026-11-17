using FlaUI.Core.AutomationElements;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace SekaiTools.UITests.Helpers;

public static class ScreenshotHelper
{
    public static string Capture(AutomationElement element, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        element.CaptureToFile(path);
        return path;
    }

    public static string CaptureStable(AutomationElement element, string directory, string name)
    {
        string? previous = null;
        var stable = 0;
        var bytes = WaitHelper.Until(() =>
        {
            using var bitmap = element.Capture();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            var current = stream.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(current));
            stable = previous == hash ? stable + 1 : 0;
            previous = hash;
            return stable >= 2 ? current : null;
        }, "固定场景截图稳定，导航动画已结束", TimeSpan.FromSeconds(5));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

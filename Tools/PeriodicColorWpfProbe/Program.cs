using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using PeriodicColorCaptureExperiment;
using SekaiToolsCore;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("PeriodicColorWpfProbe <video> <plugin> <new-report>");
        var video = Path.GetFullPath(args[0]);
        var report = Path.GetFullPath(args[2]);
        if (File.Exists(report)) throw new IOException("Do not overwrite evidence");
        var imagePath = Path.ChangeExtension(report, ".png");
        if (File.Exists(imagePath)) throw new IOException("Do not overwrite render evidence");
        static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        var inputHash = Hash(video);
        CvInvoke.NumThreads = 4;
        using var reference = new VideoCapture(video, VideoCapture.API.Ffmpeg, Tuple.Create((CapProp)70, 16));
        var interval = Math.Max(1, (int)Math.Round(reference.Get(CapProp.Fps) / 5d));
        using var owner = new ExperimentalPeriodicColorCapture(video, args[1], 16, interval);
        var previewMethod = typeof(VideoProcessor).Assembly.GetType("SekaiToolsCore.Process.FramePreview", true)!
            .GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var referenceFrame = new Mat();
        using var candidateFrame = new Mat();
        var checkedPreviews = 0;
        BitmapSource? lastRendered = null;
        for (var i = 0; i < interval * 8; i++)
        {
            if (!reference.Read(referenceFrame) || !owner.Capture.Read(candidateFrame)) throw new InvalidDataException("Read failed");
            var index = (int)reference.Get(CapProp.PosFrames);
            if (index != owner.Capture.Get(CapProp.PosFrames)) throw new InvalidDataException("Frame index differs");
            if (index % interval != 0) continue;
            if (candidateFrame.NumberOfChannels != 3) throw new InvalidDataException("Preview is not BGR");
            using var a = (Mat)previewMethod.Invoke(null, [referenceFrame])!;
            using var b = (Mat)previewMethod.Invoke(null, [candidateFrame])!;
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                // Same conversion and dispatcher placement as SubtitleTask.
                var left = a.ToBitmapSource();
                var right = b.ToBitmapSource();
                // GUI retains BitmapSource after the callback Mat is disposed.
                a.Dispose();
                b.Dispose();
                if (left.PixelWidth != right.PixelWidth || left.PixelHeight != right.PixelHeight || left.Format != right.Format)
                    throw new InvalidDataException("WPF image shape differs");
                if (!Pixels(left).SequenceEqual(Pixels(right))) throw new InvalidDataException("Converted WPF pixels differ");
                var renderedLeft = Render(left);
                var renderedRight = Render(right);
                if (!Pixels(renderedLeft).SequenceEqual(Pixels(renderedRight))) throw new InvalidDataException("Rendered WPF pixels differ");
                lastRendered = renderedRight;
            });
            checkedPreviews++;
        }
        owner.Capture.Dispose();
        if (checkedPreviews != 8 || owner.Capture.Ptr != IntPtr.Zero) throw new InvalidDataException("Coverage or release failed");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(lastRendered!));
        using (var stream = new FileStream(imagePath, FileMode.CreateNew)) encoder.Save(stream);
        if (Hash(video) != inputHash) throw new InvalidDataException("Input changed during probe");
        var wpfAssembly = typeof(BitmapSourceExtension).Assembly.Location;
        File.WriteAllText(report, JsonSerializer.Serialize(new { Video = video, InputHash = inputHash, owner.NativePath, owner.NativeHash,
            owner.PluginPath, owner.PluginHash, PreviewInterval = interval, PreviewsChecked = checkedPreviews,
            DispatcherConversionEquivalent = true, OffscreenImageRenderEquivalent = true,
            ConvertedImageSurvivesSourceDisposal = true, RenderPath = imagePath, RenderHash = Hash(imagePath),
            WpfAssemblyPath = wpfAssembly, WpfAssemblyHash = Hash(wpfAssembly),
            CaptureDisposed = true, LiveGuiQualified = false, NoPerformanceConclusion = true }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] Pixels(BitmapSource source)
    {
        var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
        var data = new byte[stride * source.PixelHeight];
        source.CopyPixels(data, stride, 0);
        return data;
    }

    private static BitmapSource Render(BitmapSource source)
    {
        var image = new Image { Source = source, Width = source.PixelWidth, Height = source.PixelHeight, Stretch = Stretch.None };
        var size = new Size(source.PixelWidth, source.PixelHeight);
        image.Measure(size);
        image.Arrange(new Rect(size));
        image.UpdateLayout();
        var rendered = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(image);
        return rendered;
    }
}

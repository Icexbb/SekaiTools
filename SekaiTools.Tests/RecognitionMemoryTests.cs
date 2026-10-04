using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Model;
using SkiaSharp;

namespace SekaiTools.Tests;

public class RecognitionMemoryTests
{
    [Fact]
    public async Task PreloaderPublishesUpcomingTemplatesAndPreservesMarkerPixels()
    {
        using var fonts = new TestFonts();
        var size = new Size(1920, 1080);
        using var manager = new TemplateManager(size, fonts);
        using var expectedManager = new TemplateManager(size, fonts);
        using var stop = new CancellationTokenSource();
        using var preloader = new TemplatePreloader(manager, new TemplateManager(size, fonts), manager.GetFontSize(),
            [new SekaiToolsBase.Story.StoryEvent.DialogStoryEvent(0, "ABCDE", 1, "ABCD", false, false)],
            [new SekaiToolsBase.Story.StoryEvent.BannerStoryEvent("Hi", 0)],
            [new SekaiToolsBase.Story.StoryEvent.MarkerStoryEvent("MV", 0)], stop.Token);
        preloader.Request(0, 0, 0);
        // Foreground demand may race with publishing the same template.
        var foreground = Task.Run(() => manager.GetMatchTemplate(TemplateUsage.DialogContent, "A"));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!manager.HasMatchTemplate(TemplateUsage.MarkerContent, "V", true))
        {
            Assert.True(DateTime.UtcNow < deadline, "Preloader did not publish");
            await Task.Delay(10);
        }
        preloader.Dispose();
        var banner = manager.GetMatchTemplate(TemplateUsage.BannerContent, "Hi");
        var expectedBanner = expectedManager.GetMatchTemplate(TemplateUsage.BannerContent, "Hi");
        Assert.Equal(0d, CvInvoke.Norm(expectedBanner.Gray, banner.Gray, NormType.L1));
        Assert.Equal(0d, CvInvoke.Norm(expectedBanner.Alpha, banner.Alpha, NormType.L1));
        Assert.Same(await foreground, manager.GetMatchTemplate(TemplateUsage.DialogContent, "A"));
        foreach (var text in new[] { "A", "AB", "ABC" })
        {
            var actual = manager.GetMatchTemplate(TemplateUsage.DialogContent, text);
            var expected = expectedManager.GetMatchTemplate(TemplateUsage.DialogContent, text);
            Assert.Equal(0d, CvInvoke.Norm(expected.Gray, actual.Gray, NormType.L1));
            Assert.Equal(0d, CvInvoke.Norm(expected.Alpha, actual.Alpha, NormType.L1));
        }
        using var source = expectedManager.CreateTemplate(TemplateUsage.MarkerContent, "MV");
        using var resized = new Mat();
        CvInvoke.Resize(source, resized, new Size((int)(source.Width * 0.9), (int)(source.Height * 0.9)));
        using var expectedMarker = new GaMat(resized);
        var marker = manager.GetMarkerMatchTemplate("MV");
        Assert.Equal(0d, CvInvoke.Norm(expectedMarker.Gray, marker.Gray, NormType.L1));
        Assert.Equal(0d, CvInvoke.Norm(expectedMarker.Alpha, marker.Alpha, NormType.L1));
    }

    [Fact]
    public void DuplicatePreparedPublicationKeepsExistingOwnerAndDisposesCandidate()
    {
        using var fonts = new TestFonts();
        using var manager = new TemplateManager(new Size(1920, 1080), fonts);
        var existing = manager.GetMatchTemplate(TemplateUsage.DialogContent, "ABC");
        using var source = manager.CreateTemplate(TemplateUsage.DialogContent, "ABC");
        var duplicate = new GaMat(source);
        Assert.Same(existing, manager.Publish(TemplateUsage.DialogContent, "ABC", duplicate, false));
        Assert.Equal(IntPtr.Zero, duplicate.Gray.Ptr);
        Assert.NotEqual(IntPtr.Zero, existing.Gray.Ptr);
    }

    [Fact]
    public void DialogReferencesReusePrefixTemplatesWithoutOwningThem()
    {
        using var fonts = new TestFonts();
        using var manager = new TemplateManager(new Size(1920, 1080), fonts);
        var path = Path.Combine(Path.GetTempPath(), $"SekaiTools-dialog-{Guid.NewGuid():N}.avi");
        try
        {
            using (var writer = new VideoWriter(path, VideoWriter.Fourcc('M', 'J', 'P', 'G'), 30, new Size(160, 90), true))
            using (var image = new Mat(90, 160, DepthType.Cv8U, 3))
            {
                image.SetTo(new MCvScalar(20));
                writer.Write(image);
            }
            using var matcher = new DialogTemplateMatcher(new VideoInfo(path), new SekaiToolsBase.Story.Story(), manager, new TemplateMatchCachePool(), null!);
            var templates = matcher.GetContentTemplates("Recognition");
            var again = matcher.GetContentTemplates("Recognition");
            Assert.Same(templates.First, again.First);
            Assert.Same(templates.Second, manager.GetMatchTemplate(TemplateUsage.DialogContent, "Re"));
            Assert.Same(templates.Third, manager.GetMatchTemplate(TemplateUsage.DialogContent, "Rec"));
            var shortText = matcher.GetContentTemplates("R");
            Assert.Same(shortText.First, shortText.Third);
            matcher.Dispose();
            Assert.NotEqual(IntPtr.Zero, templates.First.Gray.Ptr);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(3840, 2160, 960, 540)]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(1080, 1920, 304, 540)]
    [InlineData(320, 180, 320, 180)]
    public void PreviewIsBoundedAndIndependentOfRecognitionFrame(int width, int height, int expectedWidth, int expectedHeight)
    {
        using var source = new Mat(height, width, DepthType.Cv8U, 3);
        source.SetTo(new MCvScalar(30, 80, 120));
        using var preview = FramePreview.Create(source);
        Assert.Equal(new Size(expectedWidth, expectedHeight), preview.Size);
        Assert.Equal(new Size(width, height), source.Size);
        Assert.NotEqual(source.DataPointer, preview.DataPointer);
        preview.SetTo(new MCvScalar(0));
        Assert.Equal(30, CvInvoke.Mean(source).V0);
    }

    [Theory]
    [InlineData(TemplateUsage.DialogNameTag)]
    [InlineData(TemplateUsage.DialogContent)]
    [InlineData(TemplateUsage.BannerContent)]
    [InlineData(TemplateUsage.MarkerContent)]
    public void CompactTemplatesPreserveRecognitionPixelsAndReuseSmallLayers(TemplateUsage usage)
    {
        using var fonts = new TestFonts();
        using var manager = new TemplateManager(new Size(1920, 1080), fonts);
        using var original = manager.CreateTemplate(usage, "Recognition 123");
        Assert.False(original.IsSubmatrix);
        using var expected = new GaMat(original);
        var actual = manager.GetMatchTemplate(usage, "Recognition 123");
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(0, CvInvoke.Norm(expected.Gray, actual.Gray, NormType.L1));
        Assert.Equal(0, CvInvoke.Norm(expected.Alpha, actual.Alpha, NormType.L1));
        Assert.Same(actual, manager.GetMatchTemplate(usage, "Recognition 123"));
        // Disposing an independently generated source must not invalidate the cached match template.
        original.Dispose();
        Assert.Equal(0, CvInvoke.Norm(expected.Gray, actual.Gray, NormType.L1));
    }

    private sealed class TestFonts : ITemplateResourceProvider, IDisposable
    {
        private readonly string _path = Path.GetTempFileName();

        public TestFonts()
        {
            using var stream = SKTypeface.Default.OpenStream();
            var bytes = new byte[stream.Length];
            stream.Read(bytes, bytes.Length);
            File.WriteAllBytes(_path, bytes);
        }

        public string GetVideoProcessResourcePath(string fileName) => _path;
        public void Dispose() => File.Delete(_path);
    }
}


using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process;
using SekaiToolsCore.Process.Config;

namespace SekaiTools.Tests;

public class BufferedVideoReaderTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(16, 0)]
    [InlineData(4, 3)]
    public void PrefetchPreservesPixelsFrameIndicesAndTimecodes(int capacity, int startFrame)
    {
        using var video = new VideoFixture();
        using var serialCapture = new VideoCapture(video.Path);
        using var bufferedCapture = new VideoCapture(video.Path);
        serialCapture.Set(CapProp.PosFrames, startFrame);
        bufferedCapture.Set(CapProp.PosFrames, startFrame);
        using var serial = new BufferedVideoReader(serialCapture, 0, CancellationToken.None);
        var buffered = new BufferedVideoReader(bufferedCapture, capacity, CancellationToken.None);
        var frames = new List<Mat>();
        try
        {
            for (var i = startFrame; i < 8; i++)
            {
                using var expected = serial.Read();
                using var actual = buffered.Read();
                Assert.True(actual.Succeeded);
                Assert.Equal(expected.FrameIndex, actual.FrameIndex);
                Assert.Equal(i + 1, actual.FrameIndex);
                Assert.Equal(expected.Timecode, actual.Timecode);
                Assert.Equal(0d, CvInvoke.Norm(expected.Image, actual.Image, NormType.L1));
                using var gray = new Mat();
                CvInvoke.CvtColor(expected.Image, gray, ColorConversion.Bgr2Gray);
                Assert.Equal(0d, CvInvoke.Norm(gray, actual.PreparedGray!, NormType.L1));
                frames.Add(actual.Image);
            }
            using var end = buffered.Read();
            Assert.False(end.Succeeded);
        }
        finally { buffered.Dispose(); }
        Assert.All(frames, frame => Assert.Equal(IntPtr.Zero, frame.Ptr));
    }

    [Fact]
    public void PreparedGrayExchangePreservesPreviousFrameWithoutPixelCopy()
    {
        using var video = new VideoFixture();
        using var capture = new VideoCapture(video.Path);
        using var first = new FrameMatchContext();
        using var second = new FrameMatchContext();
        using var previous = new Mat();
        using var reader = new BufferedVideoReader(capture, 2, CancellationToken.None);
        for (var index = 0; index < 8; index++)
        {
            using var frame = reader.Read();
            var target = index % 2 == 0 ? first : second;
            var old = index % 2 == 0 ? second : first;
            var pointer = frame.PreparedGray!.DataPointer;
            target.UpdatePrepared(frame);
            Assert.Equal(pointer, target.Gray.DataPointer);
            if (index > 0) Assert.Equal(0d, CvInvoke.Norm(previous, old.Gray, NormType.L1));
            target.Gray.CopyTo(previous);
        }
    }

    [Fact]
    public async Task CancellingFullQueueAllowsReaderToDispose()
    {
        using var video = new VideoFixture();
        using var capture = new VideoCapture(video.Path);
        using var stop = new CancellationTokenSource();
        var reader = new BufferedVideoReader(capture, 2, stop.Token);
        using (var frame = reader.Read()) Assert.True(frame.Succeeded);
        await Task.Delay(100);
        stop.Cancel();
        await Task.Run(reader.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(1920, 1080, 0, 0)]
    [InlineData(3840, 2160, 256, 0)]
    [InlineData(1920, 1080, 4096, 16)]
    [InlineData(3840, 2160, 4096, 13)]
    public void AutomaticBudgetLimitsFrameCount(int width, int height, int availableMiB, int expected)
    {
        Assert.Equal(expected, new RecognitionPerformanceOptions { PrepareGrayFrames = true }.GetFrameCapacity(
            width, height, (long)availableMiB * 1024 * 1024));
    }

    [Fact]
    public void PerformanceModesHaveBoundedPools()
    {
        var available = 16L * 1024 * 1024 * 1024;
        Assert.Equal(4, new RecognitionPerformanceOptions { Mode = RecognitionPerformanceMode.MemorySaving }
            .GetFrameCapacity(1920, 1080, available));
        Assert.Equal(32, new RecognitionPerformanceOptions { Mode = RecognitionPerformanceMode.HighPerformance }
            .GetFrameCapacity(3840, 2160, available));
    }

    [Fact]
    public void ScaledRegionBufferIsReusedButRefreshedForNewFrame()
    {
        using var source = new Mat(90, 160, DepthType.Cv8U, 3);
        using var context = new FrameMatchContext();
        source.SetTo(new MCvScalar(20, 20, 20));
        context.Update(source);
        var region = new Rectangle(0, 0, 160, 90);
        var first = context.GetScaledGrayRoi(region, 2, Inter.Area);
        var pointer = first.DataPointer;
        source.SetTo(new MCvScalar(80, 80, 80));
        context.Update(source);
        var second = context.GetScaledGrayRoi(region, 2, Inter.Area);
        Assert.Same(first, second);
        Assert.Equal(pointer, second.DataPointer);
        Assert.Equal(80, CvInvoke.Mean(second).V0);
    }

    private sealed class VideoFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"SekaiTools-reader-{Guid.NewGuid():N}.avi");
        public VideoFixture()
        {
            using var writer = new VideoWriter(Path, VideoWriter.Fourcc('M', 'J', 'P', 'G'), 30, new Size(160, 90), true);
            Assert.True(writer.IsOpened);
            using var image = new Mat(90, 160, DepthType.Cv8U, 3);
            for (var i = 0; i < 8; i++)
            {
                image.SetTo(new MCvScalar(i * 20, 20, 60));
                CvInvoke.Rectangle(image, new Rectangle(i * 5, 10, 20, 20), new MCvScalar(255, 180, 20), -1);
                writer.Write(image);
            }
        }
        public void Dispose() => File.Delete(Path);
    }
}

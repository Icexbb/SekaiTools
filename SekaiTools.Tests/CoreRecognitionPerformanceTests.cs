using System.Diagnostics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.Model;
using SekaiToolsCore.Process.Performance;
using Matcher = SekaiToolsCore.Match.TemplateMatcher.TemplateMatcher;

namespace SekaiTools.Tests;

/// <summary>合成帧烟囱测试：同时验证核心识别结果和性能采样链路。</summary>
public sealed class CoreRecognitionPerformanceTests
{
    [Fact]
    public void RepeatedMatchingRecognizesTargetsAndReportsTiming()
    {
        using var templateSource = CreateTemplate();
        using var templateBgr = new Mat();
        CvInvoke.CvtColor(templateSource, templateBgr, ColorConversion.Bgra2Bgr);
        using var template = new GaMat(templateSource, false);
        using var context = new FrameMatchContext();
        var cache = new TemplateMatchCachePool();
        var metrics = new ProcessingPerformanceMetrics();
        var frameRegion = new Rectangle(0, 0, 160, 120);

        for (var frameIndex = 0; frameIndex < 24; frameIndex++)
        {
            using var frame = new Mat(frameRegion.Height, frameRegion.Width, DepthType.Cv8U, 3);
            frame.SetTo(new MCvScalar(12, 12, 12));
            var expected = new Point(20 + frameIndex % 6, 30 + frameIndex % 4);
            using (var target = new Mat(frame, new Rectangle(expected, templateBgr.Size)))
                templateBgr.CopyTo(target);

            var preprocessStart = Stopwatch.GetTimestamp();
            context.Update(frame);
            metrics.Record(ProcessingStage.Preprocess, Stopwatch.GetElapsedTime(preprocessStart));
            cache.SetFrameIndex(frameIndex);

            var matchStart = Stopwatch.GetTimestamp();
            var result = Matcher.Match(context, frameRegion, template, cache);
            metrics.Record(ProcessingStage.Match, Stopwatch.GetElapsedTime(matchStart));
            metrics.RecordFrame();

            Assert.Equal(expected, result.MaxLoc);
            Assert.True(result.MaxVal > 0.99);
        }

        var snapshot = metrics.Snapshot();
        Assert.Equal(24, snapshot.FrameCount);
        Assert.True(snapshot.PreprocessTime > TimeSpan.Zero);
        Assert.True(snapshot.MatchTime > TimeSpan.Zero);
        Assert.InRange(snapshot.AverageMillisecondsPerFrame, 0, 1000);
    }

    private static Mat CreateTemplate()
    {
        var template = new Mat(20, 24, DepthType.Cv8U, 4);
        template.SetTo(new MCvScalar(20, 20, 20, 255));
        CvInvoke.Rectangle(template, new Rectangle(3, 2, 7, 14),
            new MCvScalar(240, 240, 240, 255), -1);
        CvInvoke.Rectangle(template, new Rectangle(13, 6, 8, 5),
            new MCvScalar(130, 130, 130, 255), -1);
        return template;
    }
}

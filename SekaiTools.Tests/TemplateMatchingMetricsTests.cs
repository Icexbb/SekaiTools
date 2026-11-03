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

public class TemplateMatchingMetricsTests
{
    [Fact]
    public void NestedScopesRestoreStateAndDisabledMeasurementsAreEmpty()
    {
        var metrics = new TemplateMatchingMetrics();
        using (metrics.Enter(TemplateMatchingState.Tracking))
        {
            metrics.RecordRequest(TemplateMatchCachePool.MatchUsage.DialogContent3, false, Stopwatch.GetTimestamp());
        }

        Assert.Empty(metrics.Snapshot().States);
        metrics.Enabled = true;
        using (metrics.Enter(TemplateMatchingState.Lookahead))
        {
            using (metrics.Enter(TemplateMatchingState.Recovering, TemplateMatchCachePool.MatchUsage.DialogContent2))
            {
                metrics.Record(TemplateMatchingStage.Correlation, Stopwatch.GetTimestamp());
                metrics.RecordRequest(TemplateMatchCachePool.MatchUsage.DialogContent2, false,
                    Stopwatch.GetTimestamp());
            }

            Assert.Equal(TemplateMatchingState.Lookahead, metrics.CurrentState);
        }

        Assert.Equal(TemplateMatchingState.Searching, metrics.CurrentState);
        var row = Assert.Single(metrics.Snapshot().States);
        Assert.Equal("DialogContent2", row.Usage);
        Assert.Equal("Recovering", row.State);
        Assert.Equal(1, row.CorrelationCalls);
        metrics.Reset();
        Assert.Empty(metrics.Snapshot().States);
        Assert.Equal(0, metrics.Snapshot().Requests);
        Assert.Equal(0, metrics.Snapshot().CorrelationCalls);
    }

    [Fact]
    public void RealMatchingAttributesOperatorsAndCacheHitsWithoutChangingResult()
    {
        using var source = new Mat(20, 24, DepthType.Cv8U, 4);
        source.SetTo(new MCvScalar(20, 20, 20, 255));
        CvInvoke.Rectangle(source, new Rectangle(3, 2, 7, 14), new MCvScalar(240, 240, 240, 255), -1);
        using var template = new GaMat(source, false);
        using var context = new FrameMatchContext();
        context.Update(source);
        var reference = new TemplateMatchCachePool();
        var measured = new TemplateMatchCachePool();
        measured.Metrics.Enabled = true;
        reference.SetFrameIndex(1);
        measured.SetFrameIndex(1);
        var region = new Rectangle(Point.Empty, source.Size);
        var expected = Matcher.Match(context, region, template, reference,
            TemplateMatchCachePool.MatchUsage.DialogContent3);
        using (measured.Metrics.Enter(TemplateMatchingState.Tracking))
        {
            Assert.Equal(expected,
                Matcher.Match(context, region, template, measured, TemplateMatchCachePool.MatchUsage.DialogContent3));
            Assert.Equal(expected,
                Matcher.Match(context, region, template, measured, TemplateMatchCachePool.MatchUsage.DialogContent3));
        }

        var snapshot = measured.Metrics.Snapshot();
        var row = Assert.Single(snapshot.States);
        Assert.Equal("Tracking", row.State);
        Assert.Equal("DialogContent3", row.Usage);
        Assert.Equal(2, row.Requests);
        Assert.Equal(1, row.CacheHits);
        Assert.True(row.CorrelationCalls > 0);
        Assert.Equal(snapshot.Requests, row.Requests);
        Assert.Equal(snapshot.CacheHits, row.CacheHits);
        Assert.Equal(snapshot.CorrelationCalls, row.CorrelationCalls);
        Assert.Equal(snapshot.Usages.Sum(u => u.Elapsed.Ticks), row.Elapsed.Ticks);

        // The independent lookahead cache must not hide its operator cost from task totals.
        var evidence = new TemplateMatchCachePool { Metrics = measured.Metrics };
        evidence.SetFrameIndex(1);
        using (measured.Metrics.Enter(TemplateMatchingState.Lookahead))
        {
            Assert.Equal(expected, Matcher.Match(context, region, template, evidence));
        }

        var combined = measured.Metrics.Snapshot();
        Assert.Equal(3, combined.Requests);
        Assert.Equal(combined.CorrelationCalls, combined.States.Sum(s => s.CorrelationCalls));
        Assert.Contains(combined.States, s => s.State == "Lookahead" && s.Usage == "Misc" && s.CorrelationCalls > 0);
        Assert.Equal(TemplateMatchingState.Searching, measured.Metrics.CurrentState);
    }
}
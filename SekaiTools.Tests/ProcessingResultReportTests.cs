using SekaiToolsCore;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Match.TemplateMatcher;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class ProcessingResultReportTests
{
    [Theory]
    [InlineData(ProcessStopReason.None, 0, 1, ProcessingOutcome.NotStarted)]
    [InlineData(ProcessStopReason.Completed, 3, 0, ProcessingOutcome.Complete)]
    [InlineData(ProcessStopReason.Completed, 2, 1, ProcessingOutcome.Partial)]
    [InlineData(ProcessStopReason.EndOfStream, 2, 1, ProcessingOutcome.Partial)]
    [InlineData(ProcessStopReason.ReadFailed, 2, 1, ProcessingOutcome.Partial)]
    [InlineData(ProcessStopReason.ReadFailed, 0, 3, ProcessingOutcome.Failed)]
    [InlineData(ProcessStopReason.Canceled, 2, 1, ProcessingOutcome.Canceled)]
    public void 根据停止原因与识别数量判定结果状态(
        ProcessStopReason stopReason,
        int recognized,
        int unmatched,
        ProcessingOutcome expected)
    {
        Assert.Equal(expected, ProcessingResultReport.ResolveOutcome(stopReason, recognized, unmatched));
    }

    [Fact]
    public void 未识别事件关联匹配诊断并为无诊断项提供回退原因()
    {
        var frameRate = new FrameRate(30);
        var dialogs = new List<DialogBaseFrameSet>
        {
            new(new DialogStoryEvent(7, "测试台词", 1, "初音", false, false), frameRate)
        };
        var banners = new List<BannerBaseFrameSet>
        {
            new(new BannerStoryEvent("章节开始", 8), frameRate)
        };
        banners[0].Add(10);
        var markers = new List<MarkerBaseFrameSet>
        {
            new(new MarkerStoryEvent("选择项", 9), frameRate)
        };
        var diagnostics = new List<MatcherDiagnostic>
        {
            new(nameof(DialogBaseFrameSet), 0, 120, "超过有限前瞻窗口")
        };

        var report = ProcessingResultReport.Create(
            ProcessStopReason.EndOfStream, dialogs, banners, markers, diagnostics);

        Assert.Equal(ProcessingOutcome.Partial, report.Outcome);
        Assert.Equal(1, report.RecognizedTotal);
        Assert.Collection(report.UnmatchedEvents,
            item =>
            {
                Assert.Equal("对话", item.Type);
                Assert.Equal("初音：测试台词", item.Content);
                Assert.Equal("超过有限前瞻窗口", item.Reason);
            },
            item =>
            {
                Assert.Equal("标记", item.Type);
                Assert.Equal("处理结束前未识别", item.Reason);
            });
    }
}

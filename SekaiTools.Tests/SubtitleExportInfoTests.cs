using SekaiToolsCore;
using SekaiToolsCore.Process.Model;

namespace SekaiTools.Tests;

public class SubtitleExportInfoTests
{
    [Fact]
    public void 导出信息生成位于开头的注释行内容()
    {
        var info = new SubtitleExportInfo(
            "SekaiTools 自动轴机",
            "1.2.3.4",
            "已完成",
            "video.mp4",
            "script.json",
            "translation.txt");

        var comments = info.MakeComments();

        Assert.Collection(comments,
            item =>
            {
                Assert.Equal("Comment", item.Type);
                Assert.Equal("程序：SekaiTools 自动轴机；版本：1.2.3.4", item.Text);
            },
            item => Assert.Equal("任务运行状态：已完成", item.Text),
            item => Assert.Equal("使用素材：视频=video.mp4；剧本=script.json；翻译=translation.txt", item.Text));
    }

    [Fact]
    public void 部分结果注释包含统计与未识别原因()
    {
        var report = new ProcessingResultReport(
            ProcessingOutcome.Partial,
            ProcessStopReason.EndOfStream,
            2, 3,
            1, 1,
            0, 1,
            [new UnmatchedEventInfo("对话", 2, "初音：测试台词", "视频结束前未匹配")]);
        var info = new SubtitleExportInfo(
            "SekaiTools 自动轴机",
            "1.2.3.4",
            "视频已结束，存在未完成识别目标",
            "video.mp4",
            "script.json",
            "translation.txt",
            report);

        var comments = info.MakeComments();

        Assert.Equal("识别结果：结果=部分完成；对话=2/3；横幅=1/1；标记=0/1；未识别=1", comments[3].Text);
        Assert.Equal("未识别：对话[2] 初音：测试台词；原因=视频结束前未匹配", comments[4].Text);
    }
}

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
}

using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class SubtitleWorkflowTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SUBTITLE_003_取消后重启恢复并完成识别()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-SUBTITLE-003", "Functional");
        string video = "";
        app.Step("准备长对话", "合成持续120秒的对话供真实取消和恢复", () =>
        {
            ResourcePackFixture.Serve(app, server, "videoProcess", "video-resources.lock.json");
            video = RecognitionVideoFixture.Create(app.Data.Root, 120);
        });
        app.Step("取消识别", "通过终止按钮保存实际未完成进度", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Subtitle", "Subtitle.Root");
            var add = app.Finder.ById(app.Window!, "Subtitle.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "资源准备完成");
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Subtitle.AddTaskDialog");
            app.Finder.ById(dialog, "Subtitle.VideoBrowseButton").AsButton().Invoke();
            var picker = app.OwnedFileDialog();
            WaitHelper.Until(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "文件名框").AsTextBox().Text = video;
            Keyboard.Type(VirtualKeyShort.RETURN);
            var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("加入队列").And(cf.ByControlType(ControlType.Button))), "加入队列按钮");
            WaitHelper.Until(() => submit.IsEnabled, "素材校验通过");
            submit.AsButton().Invoke();
            var stop = app.Finder.ById(app.Window!, "Subtitle.StopButton");
            WaitHelper.Until(() => stop.IsEnabled, "处理已开始");
            stop.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已取消")), "取消已完成");
            var progress = Assert.Single(Directory.GetFiles(Path.Combine(app.Data.Paths.DataRoot, "SubtitleQueueProgress"), "*.json"));
            using var state = System.Text.Json.JsonDocument.Parse(File.ReadAllText(progress));
            var frame = state.RootElement.GetProperty("FrameIndex").GetInt32();
            Assert.InRange(frame, 1, 2899);
            app.Data.WriteJson("canceled-frame", new { frame });
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.Canceled");
        });
        app.Step("恢复处理", "重启时接受一次恢复机会并完成真实识别", () =>
        {
            app.Restart("Subtitle.Root");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")), "恢复提示");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("加入队列").And(cf.ByControlType(ControlType.Button))), "恢复加入队列").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已完成")), "恢复识别完成", TimeSpan.FromSeconds(60));
            Assert.Empty(Directory.GetFiles(Path.Combine(app.Data.Paths.DataRoot, "SubtitleQueueProgress"), "*.json"));
            app.Finder.ById(app.Window!, "Subtitle.ExportButton").AsButton().Invoke();
            var options = app.Finder.ById(app.Window!, "Subtitle.ExportOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "保存恢复ASS").AsButton().Invoke();
            var output = Path.Combine(app.Data.Root, "[STGenerated] recognition.ass");
            WaitHelper.Until(() => File.Exists(output) && new FileInfo(output).Length > 0, "恢复ASS写入完成");
            var line = Assert.Single(File.ReadAllLines(output), line => line.StartsWith("Dialogue:", StringComparison.Ordinal));
            Assert.Contains("识别译文", Regex.Replace(line, @"\{[^}]*\}", ""));
            var fields = line.Split(',', 10);
            Assert.InRange(TimeSpan.Parse(fields[1], CultureInfo.InvariantCulture).TotalSeconds, 0.45, 0.6);
            Assert.InRange(TimeSpan.Parse(fields[2], CultureInfo.InvariantCulture).TotalSeconds, 120.45, 120.6);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.Resumed");
        });
        app.Step("退出", "恢复完成后正常退出", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SUBTITLE_002_常规识别队列完成并导出真实ASS()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-SUBTITLE-002", "Functional");
        string video = "";
        string sourceHash = "";
        app.Step("准备素材", "固定资源及三秒合成常规对话视频就绪", () =>
        {
            ResourcePackFixture.Serve(app, server, "videoProcess", "video-resources.lock.json");
            video = RecognitionVideoFixture.Create(app.Data.Root);
            sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(video)));
        });
        app.Step("创建识别任务", "真实文件选择器载入视频和同名剧本译文", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Subtitle", "Subtitle.Root");
            var add = app.Finder.ById(app.Window!, "Subtitle.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "资源准备完成");
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Subtitle.AddTaskDialog");
            app.Finder.ById(dialog, "Subtitle.VideoBrowseButton").AsButton().Invoke();
            var picker = app.OwnedFileDialog();
            WaitHelper.Until(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "文件名框").AsTextBox().Text = video;
            Keyboard.Type(VirtualKeyShort.RETURN);
            var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("加入队列").And(cf.ByControlType(ControlType.Button))), "加入队列按钮");
            WaitHelper.Until(() => submit.IsEnabled, "素材校验通过");
            submit.AsButton().Invoke();
        });
        app.Step("识别与导出", "常规识别完成，ASS包含已识别译文事件", () =>
        {
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已完成")), "常规识别完成", TimeSpan.FromSeconds(60));
            var window = app.Window!;
            var transform = window.Patterns.Transform.Pattern;
            var originalBounds = window.BoundingRectangle;
            var timeline = app.Finder.ById(app.Window!, "Subtitle.ShowTimeline").AsCheckBox();
            var results = app.Finder.ById(app.Window!, "Subtitle.ResultList");
            Assert.True(timeline.IsChecked);
            var splitter = app.Finder.ById(app.Window!, "Subtitle.TimelineSplitter");
            var expandedHeight = results.BoundingRectangle.Height;
            splitter.Focus();
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
            WaitHelper.Until(() => results.BoundingRectangle.Height > expandedHeight, "键盘缩小时间轴后结果区域扩大");
            var bounds = splitter.BoundingRectangle;
            var beforeDrag = results.BoundingRectangle.Height;
            var center = new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            var destination = new System.Drawing.Point(center.X, center.Y - 32);
            window.SetForeground();
            WaitHelper.Until(() => window.Automation.FromPoint(center).AutomationId == "Subtitle.TimelineSplitter", "完成提示消失后可命中时间轴把手");
            FlaUI.Core.Input.Mouse.Drag(center, destination, FlaUI.Core.Input.MouseButton.Left);
            WaitHelper.Until(() => results.BoundingRectangle.Height < beforeDrag, "拖动提高时间轴高度");
            expandedHeight = results.BoundingRectangle.Height;
            timeline.IsChecked = false;
            WaitHelper.Until(() => results.BoundingRectangle.Height > expandedHeight, "折叠时间轴后结果区域扩大");
            timeline.IsChecked = true;
            WaitHelper.Until(() => results.BoundingRectangle.Height == expandedHeight, "展开时间轴后恢复布局");
            transform.Resize(originalBounds.Width, 600);
            WaitHelper.Until(() => window.BoundingRectangle.Height <= 602 && results.BoundingRectangle.Height >= 120, "短窗口保留结果列表空间");
            transform.Resize(originalBounds.Width, originalBounds.Height);
            var export = app.Finder.ById(app.Window!, "Subtitle.ExportButton");
            Assert.True(export.IsEnabled);
            export.AsButton().Invoke();
            var options = app.Finder.ById(app.Window!, "Subtitle.ExportOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "保存ASS").AsButton().Invoke();
            var output = Path.Combine(app.Data.Root, "[STGenerated] recognition.ass");
            WaitHelper.Until(() => File.Exists(output) && new FileInfo(output).Length > 0, "ASS写入完成");
            var ass = File.ReadAllText(output);
            Assert.Contains("[Events]", ass);
            Assert.Contains("Dialogue:", ass);
            Assert.Contains("识别译文", Regex.Replace(ass, @"\{[^}]*\}", ""));
            var dialogue = Assert.Single(ass.Split('\n'), line => line.StartsWith("Dialogue:", StringComparison.Ordinal));
            var fields = dialogue.Split(',', 10);
            var start = TimeSpan.Parse(fields[1], CultureInfo.InvariantCulture).TotalSeconds;
            var end = TimeSpan.Parse(fields[2], CultureInfo.InvariantCulture).TotalSeconds;
            Assert.InRange(start, 0.45, 0.6);
            Assert.InRange(end, 2.45, 2.6);
            Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(video))));
            app.Data.WriteJson("recognized-interval", new { start, end, sourceHash });
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.Recognized");
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "识别输出后正常退出", app.Close);
        app.Step("重启加载历史", "独立进程从已完成历史加载已有识别结果并再次导出", () =>
        {
            var historyFile = Assert.Single(Directory.GetFiles(Path.Combine(app.Data.Paths.DataRoot, "History"), "*.json"));
            var historyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(historyFile)));
            var output = Path.Combine(app.Data.Root, "[STGenerated] recognition.ass");
            var originalDialogue = File.ReadAllLines(output).Where(line => line.StartsWith("Dialogue:", StringComparison.Ordinal)).ToArray();
            File.Move(output, Path.Combine(app.Data.Root, "first-export.ass"));
            app.Restart("Subtitle.Root");
            app.Navigate("MainWindow.Navigation.Subtitle", "Subtitle.Root");
            var historyButton = app.Finder.ById(app.Window!, "Subtitle.HistoryButton");
            WaitHelper.Until(() => historyButton.IsEnabled, "资源准备完成");
            historyButton.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Subtitle.HistoryDialog");
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("加载 recognition.mp4").And(cf.ByControlType(ControlType.Button))), "已完成历史加载入口").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("历史任务")), "历史任务分组");
            var export = app.Finder.ById(app.Window!, "Subtitle.ExportButton");
            WaitHelper.Until(() => export.IsEnabled, "历史结果允许导出");
            export.AsButton().Invoke();
            var options = app.Finder.ById(app.Window!, "Subtitle.ExportOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "再次保存ASS").AsButton().Invoke();
            WaitHelper.Until(() => File.Exists(output) && new FileInfo(output).Length > 0, "历史ASS写入完成");
            Assert.Equal(originalDialogue, File.ReadAllLines(output).Where(line => line.StartsWith("Dialogue:", StringComparison.Ordinal)).ToArray());
            Assert.Equal(historyHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(historyFile))));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(historyFile)!, "*.json"));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.HistoryRestored");
        });
        app.Step("历史退出", "加载历史后正常退出", app.Close);
    }
}

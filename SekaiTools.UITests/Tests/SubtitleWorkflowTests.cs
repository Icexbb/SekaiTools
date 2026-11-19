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
        string checkpointPath = "";
        byte[] checkpointBytes = [];
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
            var window = app.Window!;
            app.Finder.ById(window, "Subtitle.ExpandTimeline").AsButton().Invoke();
            var readOnlyBadge = WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("处理中 · 只读")),
                "处理中可展开时间轴并看到只读提示");
            Assert.False(readOnlyBadge.IsOffscreen);
            Assert.False(app.Finder.ById(window, "Subtitle.Timeline.StartMinus").IsEnabled);
            Assert.False(app.Finder.ById(window, "Subtitle.ExportButton").IsEnabled);
            stop.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已取消")), "取消已完成");
            var progress = Assert.Single(Directory.GetFiles(Path.Combine(app.Data.Paths.DataRoot, "SubtitleQueueProgress"), "*.json"));
            checkpointPath = progress;
            checkpointBytes = File.ReadAllBytes(progress);
            using var state = System.Text.Json.JsonDocument.Parse(checkpointBytes);
            var frame = state.RootElement.GetProperty("FrameIndex").GetInt32();
            Assert.InRange(frame, 1, 2899);
            app.Data.WriteJson("canceled-frame", new { frame });
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.Canceled");
        });
        app.Step("恢复处理", "重启时接受一次恢复机会并完成真实识别", () =>
        {
            app.Restart("Subtitle.Root");
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")), "恢复提示");
            Assert.False(File.Exists(checkpointPath));
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
        app.Step("关闭正在恢复的任务", "关闭会停止恢复中的任务，重启不再提示恢复", () =>
        {
            var historyDirectory = Path.Combine(app.Data.Paths.DataRoot, "History");
            var history = Assert.Single(Directory.GetFiles(historyDirectory, "*.json"));
            var originalHistory = File.ReadAllBytes(history);
            File.WriteAllBytes(checkpointPath, checkpointBytes);
            app.Restart("Subtitle.Root");
            var window = app.Window!;
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")), "独立检查点副本触发恢复提示");
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("加入队列").And(cf.ByControlType(ControlType.Button))), "恢复加入队列").AsButton().Invoke();
            var stop = app.Finder.ById(window, "Subtitle.StopButton");
            WaitHelper.Until(() => stop.IsEnabled, "恢复任务已开始处理");
            app.Close();

            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(checkpointPath)!, "*.json"));
            Assert.Equal(history, Assert.Single(Directory.GetFiles(historyDirectory, "*.json")));
            Assert.Equal(originalHistory, File.ReadAllBytes(history));
            app.Restart("Subtitle.Root");
            window = app.Window!;
            var add = app.Finder.ById(window, "Subtitle.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "恢复中关闭后的字幕页可添加任务");
            Assert.Null(window.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")));
        });
        app.Step("放弃恢复", "放弃独立副本后重启不再提示，已完成历史保持原样", () =>
        {
            var historyDirectory = Path.Combine(app.Data.Paths.DataRoot, "History");
            var history = Assert.Single(Directory.GetFiles(historyDirectory, "*.json"));
            var originalHistory = File.ReadAllBytes(history);
            File.WriteAllBytes(checkpointPath, checkpointBytes);
            app.Restart("Subtitle.Root");
            var window = app.Window!;
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")), "独立检查点副本触发恢复提示");
            Assert.False(File.Exists(checkpointPath));
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("放弃恢复").And(cf.ByControlType(ControlType.Button))), "放弃恢复入口").AsButton().Invoke();
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")) is null, "恢复提示关闭");
            app.Restart("Subtitle.Root");
            window = app.Window!;
            var add = app.Finder.ById(window, "Subtitle.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "放弃后的字幕页可添加任务");
            Assert.Null(window.FindFirstDescendant(cf => cf.ByName("恢复未完成任务")));
            Assert.False(File.Exists(checkpointPath));
            Assert.Equal(history, Assert.Single(Directory.GetFiles(historyDirectory, "*.json")));
            Assert.Equal(originalHistory, File.ReadAllBytes(history));
        });
        app.Step("退出", "恢复验证后正常退出", app.Close);
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
            var results = app.Finder.ById(window, "Subtitle.ResultList");
            var viewport = app.Finder.ById(window, "Subtitle.TimelineViewport");
            var expand = app.Finder.ById(window, "Subtitle.ExpandTimeline");
            var collapsedHeight = results.BoundingRectangle.Height;
            expand.AsButton().Invoke();
            var collapse = app.Finder.ById(window, "Subtitle.CollapseTimeline");
            WaitHelper.Until(() => results.BoundingRectangle.Height < collapsedHeight, "展开器为时间轴分配空间");
            var grip = app.Finder.ById(window, "Subtitle.TimelineSplitter");
            var initialHeight = viewport.BoundingRectangle.Height;
            grip.Focus();
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.UP);
            WaitHelper.Until(() => viewport.BoundingRectangle.Height > initialHeight, "方向键拉高时间轴");
            var gripBounds = grip.BoundingRectangle;
            var center = new System.Drawing.Point(gripBounds.Left + gripBounds.Width / 2, gripBounds.Top + gripBounds.Height / 2);
            WaitHelper.Until(() => window.Automation.FromPoint(center).AutomationId == "Subtitle.TimelineSplitter", "完成提示消失后可命中把手");
            var beforeDrag = viewport.BoundingRectangle.Height;
            FlaUI.Core.Input.Mouse.Drag(center, new System.Drawing.Point(center.X, center.Y - 72), FlaUI.Core.Input.MouseButton.Left);
            WaitHelper.Until(() => viewport.BoundingRectangle.Height > beforeDrag, "拖动拉高时间轴");
            var resizedHeight = viewport.BoundingRectangle.Height;
            Assert.InRange(window.BoundingRectangle.Bottom - viewport.BoundingRectangle.Bottom, 0, 24);
            WaitHelper.Until(() => collapse.BoundingRectangle.Top - viewport.BoundingRectangle.Top is >= 0 and <= 24,
                "拉高后工具栏仍贴近时间轴顶部");
            ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.TimelineExpanded");
            collapse.AsButton().Invoke();
            WaitHelper.Until(() => results.BoundingRectangle.Height == collapsedHeight, "折叠后结果列表恢复高度");
            app.Finder.ById(window, "Subtitle.ExpandTimeline").AsButton().Invoke();
            WaitHelper.Until(() => viewport.BoundingRectangle.Height == resizedHeight, "展开后恢复调整高度");
            transform.Resize(originalBounds.Width, 600);
            WaitHelper.Until(() => window.BoundingRectangle.Height <= 602 && results.BoundingRectangle.Height >= 120, "短窗口保留结果列表空间");
            app.Finder.ById(window, "Subtitle.ExpandTimeline");
            transform.Resize(originalBounds.Width, originalBounds.Height);
            var edit = app.Finder.ById(results, "LineEditBtn");
            edit.AsButton().Invoke();
            var startTime = app.Finder.ById(window, "Subtitle.Timeline.StartTime").AsTextBox();
            var startMinus = app.Finder.ById(window, "Subtitle.Timeline.StartMinus").AsButton();
            WaitHelper.Until(() => startMinus.IsEnabled, "从对话结果定位可编辑的时间轴事件");
            var originalStart = startTime.Text;
            var undo = WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("撤销时间轴调整")
                .And(cf.ByControlType(ControlType.Button))), "撤销时间轴调整按钮").AsButton();
            Assert.False(undo.IsEnabled);
            startMinus.Invoke();
            WaitHelper.Until(() => undo.IsEnabled && startTime.Text != originalStart, "起始时间提前一帧并可撤销");
            var earlier = TimeSpan.Parse(startTime.Text, CultureInfo.InvariantCulture);
            var original = TimeSpan.Parse(originalStart, CultureInfo.InvariantCulture);
            Assert.InRange((original - earlier).TotalMilliseconds, 30, 50);
            var adjustedStart = startTime.Text;
            var restoreTiming = app.Finder.ById(window, "RestoreTimingButton").AsButton();
            WaitHelper.Until(() => restoreTiming.IsEnabled, "微调后可以还原识别时长");
            restoreTiming.Invoke();
            WaitHelper.Until(() => startTime.Text == originalStart && !restoreTiming.IsEnabled, "还原识别时长恢复起始时间");
            undo.Invoke();
            WaitHelper.Until(() => startTime.Text == adjustedStart && restoreTiming.IsEnabled, "撤销还原后回到微调时序");
            undo.Invoke();
            WaitHelper.Until(() => startTime.Text == originalStart && !undo.IsEnabled, "撤销恢复识别时序");
            var endTime = app.Finder.ById(window, "EndTimeBox").AsTextBox();
            var originalEnd = endTime.Text;
            var preview = WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("预览当前事件")
                .And(cf.ByControlType(ControlType.Button))), "预览当前事件").AsButton();
            WaitHelper.Until(() => preview.IsEnabled, "当前事件可以预览");
            preview.Invoke();
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("播放完成")), "当前事件预览完成");
            var playhead = app.Finder.ById(window, "Subtitle.Timeline.Playhead");
            var playheadBounds = playhead.BoundingRectangle;
            var playheadCenter = new System.Drawing.Point(playheadBounds.Left + playheadBounds.Width / 2, playheadBounds.Top + playheadBounds.Height / 2);
            Mouse.Drag(playheadCenter, new System.Drawing.Point(playheadCenter.X - 50, playheadCenter.Y), MouseButton.Left);
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("已定位")), "拖动播放光标完成定位");
            Assert.True(playhead.BoundingRectangle.Left < playheadBounds.Left);
            Assert.Equal(originalStart, startTime.Text);
            Assert.Equal(originalEnd, endTime.Text);
            Assert.False(undo.IsEnabled);
            var export = app.Finder.ById(app.Window!, "Subtitle.ExportButton");
            Assert.True(export.IsEnabled);
            var output = Path.Combine(app.Data.Root, "[STGenerated] recognition.ass");
            File.WriteAllText(output, "Existing ASS");
            var existingOutput = File.ReadAllBytes(output);
            var historyDirectory = Path.Combine(app.Data.Paths.DataRoot, "History");
            WaitHelper.Until(() => Directory.Exists(historyDirectory) && Directory.GetFiles(historyDirectory, "*.json").Length == 1,
                "已完成历史已保存");
            var completedHistory = Assert.Single(Directory.GetFiles(historyDirectory, "*.json"));
            var checkpointDirectory = Directory.CreateDirectory(Path.Combine(app.Data.Paths.DataRoot, "SubtitleQueueProgress"));
            var saveKey = Path.GetFileName(completedHistory).Split('_')[^1];
            var checkpoint = Path.Combine(checkpointDirectory.FullName, saveKey);
            // 正常完成会清理检查点。用已保存的状态模拟同一任务残留的检查点。
            using var history = System.Text.Json.JsonDocument.Parse(File.ReadAllText(completedHistory));
            File.WriteAllText(checkpoint, history.RootElement.GetProperty("State").GetRawText());
            var checkpointBeforeCancel = File.ReadAllBytes(checkpoint);
            export.AsButton().Invoke();
            var options = app.Finder.ById(app.Window!, "Subtitle.ExportOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "保存ASS").AsButton().Invoke();
            var confirmation = app.Finder.ById(window, "Export.OverwriteConfirmation");
            WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("取消").And(cf.ByControlType(ControlType.Button))), "取消覆盖").AsButton().Invoke();
            WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByAutomationId("Export.OverwriteConfirmation")) is null, "覆盖确认关闭");
            Assert.Equal(existingOutput, File.ReadAllBytes(output));
            Assert.Equal(checkpointBeforeCancel, File.ReadAllBytes(checkpoint));

            export.AsButton().Invoke();
            options = app.Finder.ById(window, "Subtitle.ExportOptions");
            WaitHelper.Until(() => options.FindFirstDescendant(cf => cf.ByName("保存").And(cf.ByControlType(ControlType.Button))), "重新保存ASS").AsButton().Invoke();
            confirmation = app.Finder.ById(window, "Export.OverwriteConfirmation");
            WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("覆盖").And(cf.ByControlType(ControlType.Button))), "确认覆盖").AsButton().Invoke();
            WaitHelper.Until(() => !File.Exists(checkpoint), "保存ASS后清理检查点");
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

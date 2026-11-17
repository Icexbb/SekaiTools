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
    }
}

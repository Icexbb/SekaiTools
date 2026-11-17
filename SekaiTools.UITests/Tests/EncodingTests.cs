using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class EncodingTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_001_真实资源准备和小视频转码完成()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-ENCODING-001", "Functional");
        var executable = Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_EXE")!;
        var pack = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "fixtures", "vapourSynth"));
        var files = new Dictionary<string, byte[]>();
        var manifest = new List<object>();
        var video = Path.Combine(app.Data.Root, "input.mp4");
        app.Step("资源及素材", "固定版本真实运行库和两秒合成视频就绪", () =>
        {
            using var resourceLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "media-resources.lock.json")));
            foreach (var entry in resourceLock.RootElement.EnumerateArray())
            {
                var name = entry.GetProperty("Name").GetString()!;
                var path = Path.GetFullPath(Path.Combine(pack, name));
                Assert.True(SekaiToolsConfiguration.ApplicationPaths.ContainsPath(pack, path));
                Assert.True(File.Exists(path), "请先运行 Tools/PrepareUIMediaResources.ps1 -SourceDirectory <运行库目录>。");
                var bytes = File.ReadAllBytes(path);
                Assert.Equal(entry.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
                files.Add(name, bytes);
                manifest.Add(new { Path = "vapourSynth/" + name, Size = bytes.Length, Md5 = Convert.ToHexString(MD5.HashData(bytes)) });
            }
            var config = Path.Combine(app.Data.Root, "network-endpoints.json");
            File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
            server.Response = request => request.Contains("/resources/vapourSynth.json ") ? JsonSerializer.Serialize(manifest) : null;
            server.BinaryResponse = request => files.FirstOrDefault(file => request.Contains("/resources/vapourSynth/" + file.Key + " ")).Value;
            FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), Path.Combine(app.Data.Directory, "generate-video.log"),
                "-y", "-f", "lavfi", "-i", "color=c=blue:s=320x240:r=24", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", video);
        });
        var inputHash = SHA256.HashData(File.ReadAllBytes(video));
        app.Step("资源准备与创建任务", "真实页面下载校验运行库并完成素材分析", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Encoding", "Encoding.Root");
            var add = app.Finder.ById(app.Window!, "Encoding.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "压制运行库准备完成", TimeSpan.FromSeconds(60));
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Encoding.AddTaskDialog");
            app.Finder.ById(dialog, "Encoding.VideoBrowseButton").AsButton().Invoke();
            var picker = app.OwnedFileDialog();
            WaitHelper.Until(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "视频文件名框").AsTextBox().Text = video;
            Keyboard.Type(VirtualKeyShort.RETURN);
            var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("添加任务").And(cf.ByControlType(ControlType.Button))), "添加任务按钮");
            WaitHelper.Until(() => submit.IsEnabled, "小视频分析与提交校验通过", TimeSpan.FromSeconds(30));
            submit.AsButton().Invoke();
        });
        app.Step("真实转码", "队列任务完成且输出能被FFmpeg完整解码，源文件不变", () =>
        {
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已完成")), "真实转码完成", TimeSpan.FromSeconds(60));
            var output = Path.Combine(app.Data.Root, "[STVS]input.mp4");
            Assert.True(new FileInfo(output).Length > 0);
            Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(video)));
            var decodeLog = Path.Combine(app.Data.Directory, "decode-output.log");
            FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), decodeLog, "-v", "error", "-i", output, "-map", "0:v:0", "-progress", "pipe:1", "-f", "null", "-");
            Assert.Contains("frame=48", File.ReadAllText(decodeLog));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Encoding.Completed");
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "转码后正常退出", app.Close);
    }
}

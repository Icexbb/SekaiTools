using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class SubtitleResourceTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SUBTITLE_001_真实资源下载校验后可打开任务表单()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-SUBTITLE-001", "Functional");
        var files = new Dictionary<string, byte[]>();
        var manifest = new List<object>();
        app.Step("准备固定资源", "四个真实资源符合锁定哈希，仅读取本地fixture副本", () =>
        {
            var executable = Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_EXE")!;
            var pack = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "fixtures", "videoProcess"));
            using var resourceLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "video-resources.lock.json")));
            foreach (var entry in resourceLock.RootElement.EnumerateArray())
            {
                var name = entry.GetProperty("Name").GetString()!;
                var path = Path.Combine(pack, name);
                Assert.True(File.Exists(path), "缺少固定资源包，请运行 Tools/PrepareUIVideoResources.ps1 -SourceDirectory <资源目录>。");
                var bytes = File.ReadAllBytes(path);
                Assert.Equal(entry.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
                files.Add(name, bytes);
                manifest.Add(new { Path = "videoProcess/" + name, Size = bytes.Length, Md5 = Convert.ToHexString(MD5.HashData(bytes)) });
            }
            var config = Path.Combine(app.Data.Root, "network-endpoints.json");
            File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
            server.Response = request => request.Contains("/resources/videoProcess.json ") ? JsonSerializer.Serialize(manifest) : null;
            server.BinaryResponse = request => files.FirstOrDefault(file => request.Contains("/resources/videoProcess/" + file.Key + " ")).Value;
        });
        app.Step("启动与资源准备", "真实资源管理器从本地HTTP下载并校验全部文件", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Subtitle", "Subtitle.Root");
            var add = app.Finder.ById(app.Window!, "Subtitle.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "字幕资源准备完成", TimeSpan.FromSeconds(30));
            foreach (var file in files)
            {
                var downloaded = Path.Combine(app.Data.Paths.DataRoot, "Resource", "videoProcess", file.Key);
                Assert.Equal(SHA256.HashData(file.Value), SHA256.HashData(File.ReadAllBytes(downloaded)));
                Assert.Contains(server.Requests, request => request.Contains("/resources/videoProcess/" + file.Key + " "));
            }
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Subtitle.AddTaskDialog");
            Assert.DoesNotContain(dialog.FindAllDescendants(cf => cf.ByName("加入队列").And(cf.ByControlType(ControlType.Button))), button => button.IsEnabled);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Subtitle.AddTask");
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("取消").And(cf.ByControlType(ControlType.Button))), "取消添加任务").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Subtitle.AddTaskDialog")) is null, "任务表单关闭");
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "正常退出", app.Close);
    }
}

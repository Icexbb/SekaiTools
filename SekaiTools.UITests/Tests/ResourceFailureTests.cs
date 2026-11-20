using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class ResourceFailureTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_004_运行库不可用后可重试并添加任务()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-ENCODING-004", "Functional");
        ResourcePackFixture.Serve(app, server, "vapourSynth", "media-resources.lock.json");
        var manifest = server.Response;
        server.Response = _ => null;
        app.Step("资源不可用", "资源服务返回 503，压制页禁用添加并提示重试", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Encoding", "Encoding.Root");
            var window = app.Window!;
            var add = app.Finder.ById(window, "Encoding.AddTaskButton");
            WaitHelper.Until(() => window.FindFirstDescendant(cf =>
                cf.ByName("检查设置").And(cf.ByControlType(ControlType.Button))), "资源不可用对话框", TimeSpan.FromSeconds(30));
            Assert.False(add.IsEnabled);
            Assert.Contains(server.Requests, request => request.Contains("/resources/vapourSynth.json ", StringComparison.Ordinal));
            ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), "Encoding.ResourceUnavailable");
        });
        app.Step("重试准备运行库", "恢复资源服务后重新请求清单并解锁添加表单", () =>
        {
            var window = app.Window!;
            var requestCount = server.Requests.Count;
            server.Response = manifest;
            WaitHelper.Until(() => window.FindFirstDescendant(cf =>
                cf.ByName("重试").And(cf.ByControlType(ControlType.Button))), "资源失败的重试按钮").AsButton().Invoke();
            var add = app.Finder.ById(window, "Encoding.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "重试后运行库准备完成", TimeSpan.FromSeconds(60));
            Assert.Contains(server.Requests.Skip(requestCount), request => request.Contains("/resources/vapourSynth/ffmpeg.exe ", StringComparison.Ordinal));
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(window, "Encoding.AddTaskDialog");
            WaitHelper.Until(() => dialog.FindFirstDescendant(cf =>
                cf.ByName("取消").And(cf.ByControlType(ControlType.Button))), "取消添加任务").AsButton().Invoke();
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "重试后正常退出", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_RESOURCE_001_资源服务失败后可进入设置并正常退出()
    {
        foreach (var page in new[] { "Subtitle", "Encoding" })
        {
            using var server = new LoopbackHttpFixture();
            using var app = new ApplicationFixture($"UI-RESOURCE-001-{page}", "Functional");
            var config = Path.Combine(app.Data.Root, "network-endpoints.json");
            File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
            app.Step("启动", "隔离工作区就绪", app.Start);
            app.Step("资源服务失败", "真实页面处理本地 HTTP 503 并提供检查设置入口", () =>
            {
                app.Navigate($"MainWindow.Navigation.{page}", page == "Subtitle" ? "Subtitle.Root" : "Encoding.Root");
                var settings = WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf =>
                    cf.ByName("检查设置").And(cf.ByControlType(ControlType.Button))), "资源失败对话框的检查设置按钮", TimeSpan.FromSeconds(30));
                Assert.NotEmpty(server.Requests);
                ScreenshotHelper.Capture(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), page + ".ResourceFailure");
                settings.AsButton().Invoke();
                app.Finder.ById(app.Window!, Catalog.Settings);
                app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
            });
            app.Step("退出", "失败后正常关闭，无资源任务残留", app.Close);
        }
    }
}

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
    public void UI_RESOURCE_001_资源服务失败后可进入设置并正常退出()
    {
        foreach (var page in new[] { "Subtitle", "Encoding" })
        {
            using var server = new ResourceFailureServer();
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

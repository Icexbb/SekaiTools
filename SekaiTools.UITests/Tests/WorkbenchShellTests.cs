using FlaUI.Core.AutomationElements;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchShellTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_002_外壳页头导航与紧凑工具栏()
    {
        using var app = new ApplicationFixture("UI-DESIGN-002", "Functional");
        app.Step("启动", "翻译工作区和公共页头就绪", app.Start);
        app.Step("页头跟随导航", "实际页面、选中项与公共标题一致", () =>
        {
            AssertTitle("脚本翻译");
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            AssertTitle("设置与关于");
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            AssertTitle("脚本翻译");
        });
        app.Step("紧凑外壳", "960×600时导航开合和日志入口可操作", () =>
        {
            app.Window!.Patterns.Transform.Pattern.Resize(960, 600);
            WaitHelper.Until(() => app.Window.BoundingRectangle.Width <= 970, "窗口缩到最小宽度");
            var toggle = app.Finder.ById(app.Window, Catalog.ToggleNavigation).AsButton();
            Assert.False(toggle.IsOffscreen);
            Assert.Equal("展开导航", toggle.Name);
            toggle.Invoke();
            WaitHelper.Until(() => toggle.Name == "收起导航", "导航已展开");
            ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), "Shell.Narrow.NavigationOpen");
            toggle.Invoke();
            WaitHelper.Until(() => toggle.Name == "展开导航", "导航已收起");
            var logs = app.Finder.ById(app.Window, Catalog.OpenLogs).AsButton();
            Assert.False(logs.IsOffscreen);
            Assert.InRange(logs.BoundingRectangle.Right, app.Window.BoundingRectangle.Left, app.Window.BoundingRectangle.Right);
            logs.Invoke();
            var logWindow = app.OwnedWindow("RuntimeLog.Root");
            Assert.Equal("运行日志", logWindow.Title);
            logWindow.Close();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            AssertTitle("设置与关于");
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), "Shell.Narrow.Translation");
        });
        app.Step("退出", "紧凑工作区正常退出", app.Close);

        void AssertTitle(string expected)
        {
            var header = app.Finder.ById(app.Window!, Catalog.PageHeader);
            WaitHelper.Until(() => app.Finder.ById(header, "Workbench.Header.Title").Name == expected, "公共页头更新");
        }
    }
}

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
    public void UI_DESIGN_005_外壳三主题三尺寸组合()
    {
        using var app = new ApplicationFixture("UI-DESIGN-005", "Functional");
        app.Step("启动", "隔离外壳就绪", app.Start);
        var cases = new List<object>();
        app.Step("外壳矩阵", "实际主题、紧凑导航及日志入口在各尺寸可用", () =>
        {
            var window = app.Window!;
            var desktop = window.Automation.GetDesktop().BoundingRectangle;
            try
            {
                foreach (var (text, theme) in new[] { ("亮色", "Light"), ("暗色", "Dark"), ("高对比度", "HighContrast") })
                {
                    app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
                    var choice = app.Finder.ById(window, "Settings.Field.CurrentApplicationTheme").AsComboBox();
                    choice.Focus();
                    choice.Expand();
                    WaitHelper.Until(() => choice.Items.FirstOrDefault(item => item.Text == text), "主题选项就绪")
                        .Patterns.SelectionItem.Pattern.Select();
                    choice.Collapse();
                    WaitHelper.Until(() => choice.SelectedItem?.Text == text, "真实设置主题已切换");
                    app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
                    foreach (var (width, height) in new[] { (960, 600), (1280, 960), (1920, 1080) })
                    {
                        if (desktop.Width < width + 24 || desktop.Height < height + 24)
                            throw new InvalidOperationException($"外壳验证环境不满足：屏幕不足以容纳 {width}×{height}。");
                        window.Patterns.Transform.Pattern.Resize(width, height);
                        window.Patterns.Transform.Pattern.Move(desktop.Left + (desktop.Width - width) / 2, desktop.Top + (desktop.Height - height) / 2);
                        WaitHelper.Until(() => Math.Abs(window.BoundingRectangle.Width - width) <= 1 && Math.Abs(window.BoundingRectangle.Height - height) <= 1, "实际外壳尺寸");
                        AssertWorkspace();
                        var toggle = app.Finder.ById(window, Catalog.ToggleNavigation).AsButton();
                        var logs = app.Finder.ById(window, Catalog.OpenLogs).AsButton();
                        VisibleInside(toggle);
                        VisibleInside(logs);
                        if (toggle.Name == "收起导航") toggle.Invoke();
                        WaitHelper.Until(() => toggle.Name == "展开导航", "导航收起");
                        toggle.Invoke();
                        WaitHelper.Until(() => toggle.Name == "收起导航", "导航展开");
                        foreach (var id in new[] { Catalog.TranslationNavigation, Catalog.SettingsNavigation })
                            VisibleInside(app.Finder.ById(window, id));
                        var prefix = $"{theme}.{width}x{height}";
                        ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".NavigationOpen");
                        toggle.Invoke();
                        WaitHelper.Until(() => toggle.Name == "展开导航", "恢复编辑区");
                        foreach (var button in new[] { toggle, logs })
                        {
                            WaitHelper.Until(() => button.BoundingRectangle.Width <= 44, "收起后的侧栏按钮完成布局");
                            Assert.InRange(button.BoundingRectangle.Width, 28, 44);
                            var contentLeft = app.Finder.ById(window, Catalog.Translation).BoundingRectangle.Left;
                            Assert.True(button.BoundingRectangle.Right <= contentLeft - 4,
                                "收起后的按钮必须完整落在侧栏内部，不能延伸到容器裁切边缘。");
                        }
                        logs.Invoke();
                        var logWindow = app.OwnedWindow("RuntimeLog.Root");
                        Assert.Equal("运行日志", logWindow.Title);
                        logWindow.Close();
                        FlaUI.Core.Input.Mouse.MoveTo((int)window.BoundingRectangle.Right - 30, (int)window.BoundingRectangle.Bottom - 30);
                        var screenshot = ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".Workspace");
                        using var bitmap = new System.Drawing.Bitmap(screenshot);
                        var background = bitmap.GetPixel(width - 10, height / 2);
                        if (theme == "Dark") Assert.True(background.R < 70 && background.G < 70 && background.B < 70, "工作区应保持暗色。");
                        if (theme == "Light") Assert.True(background.R > 230 && background.G > 230 && background.B > 230, "工作区应保持浅色。");
                        cases.Add(new { Theme = theme, Width = width, Height = height, Background = background.ToArgb().ToString("X8"), Result = "Passed" });
                    }
                }
            }
            finally { app.Data.WriteJson("shell-matrix", cases); }

            void AssertWorkspace()
            {
                Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.PageHeader")));
                var page = app.Finder.ById(window, Catalog.Translation).BoundingRectangle;
                Assert.InRange(page.Top, window.BoundingRectangle.Top + 35, window.BoundingRectangle.Top + 60);
                Assert.True(page.Height >= window.BoundingRectangle.Height - 65, "工作区应紧接标题栏，不能再保留独立页头的空白高度。");
            }

            void VisibleInside(AutomationElement element)
            {
                Assert.False(element.IsOffscreen);
                var bounds = element.BoundingRectangle;
                Assert.True(bounds.Width > 0 && bounds.Height > 0);
                Assert.InRange(bounds.Left, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                Assert.InRange(bounds.Right, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                Assert.InRange(bounds.Bottom, window.BoundingRectangle.Top, window.BoundingRectangle.Bottom);
            }
        });
        app.Step("退出", "正常关闭所有自有窗口", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_002_紧凑外壳导航与日志()
    {
        using var app = new ApplicationFixture("UI-DESIGN-002", "Functional");
        app.Step("启动", "翻译工作区就绪", app.Start);
        app.Step("导航保持当前页面", "实际页面与导航选中项一致", () =>
        {
            AssertPage(Catalog.TranslationNavigation, Catalog.Translation);
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            AssertPage(Catalog.SettingsNavigation, Catalog.Settings);
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            AssertPage(Catalog.TranslationNavigation, Catalog.Translation);
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
            AssertPage(Catalog.SettingsNavigation, Catalog.Settings);
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            ScreenshotHelper.CaptureStable(app.Window, Path.Combine(app.Data.Directory, "Screenshots"), "Shell.Narrow.Translation");
        });
        app.Step("退出", "紧凑工作区正常退出", app.Close);

        void AssertPage(string navigation, string page)
        {
            app.Finder.ById(app.Window!, page);
            WaitHelper.Until(() => app.Finder.ById(app.Window!, navigation).Properties.ItemStatus.Value == "当前页面", "导航选中项更新");
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("MainWindow.PageHeader")));
        }
    }
}

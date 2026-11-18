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
        app.Step("外壳矩阵", "实际主题、页头、导航开合及日志入口在各尺寸可用", () =>
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
                        var header = app.Finder.ById(window, Catalog.PageHeader);
                        var title = app.Finder.ById(header, "Workbench.Header.Title");
                        Assert.Equal("脚本翻译", title.Name);
                        VisibleInside(title);
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
                        logs.Invoke();
                        var logWindow = app.OwnedWindow("RuntimeLog.Root");
                        Assert.Equal("运行日志", logWindow.Title);
                        logWindow.Close();
                        var screenshot = ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".Workspace");
                        using var bitmap = new System.Drawing.Bitmap(screenshot);
                        var background = bitmap.GetPixel(10, 50);
                        if (theme == "Dark") Assert.True(background.R < 70 && background.G < 70 && background.B < 70, "外壳页头应使用暗色工作区画刷。");
                        if (theme == "Light") Assert.True(background.R > 230 && background.G > 230 && background.B > 230, "外壳页头应使用浅色工作区画刷。");
                        cases.Add(new { Theme = theme, Width = width, Height = height, Background = background.ToArgb().ToString("X8"), Result = "Passed" });
                    }
                }
            }
            finally { app.Data.WriteJson("shell-matrix", cases); }

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

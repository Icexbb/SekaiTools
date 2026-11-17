using FlaUI.Core.AutomationElements;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class SettingsTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SETTINGS_001_主题自动保存并跨进程重载()
    {
        using var app = new ApplicationFixture("UI-SETTINGS-001", "Functional");
        const string theme = "Settings.Field.CurrentApplicationTheme";
        app.Step("启动", "隔离翻译工作区就绪", app.Start);
        app.Step("修改主题", "通过真实设置控件切换暗色并自动保存", () =>
        {
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            app.Finder.ById(app.Window!, theme).AsComboBox().Select(1);
            app.Finder.ById(app.Window!, theme).AsComboBox().Collapse();
            WaitHelper.Until(() => File.Exists(Path.Combine(app.Data.Paths.DataRoot, "Data", "setting.json")), "设置文件生成");
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.Dark");
        });
        app.Step("重启重载", "新进程从同一隔离配置恢复暗色选择", () =>
        {
            app.Restart();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            Assert.Equal("暗色", app.Finder.ById(app.Window!, theme).AsComboBox().SelectedItem?.Text);
            app.Finder.ById(app.Window!, theme).AsComboBox().Select(0);
            app.Finder.ById(app.Window!, theme).AsComboBox().Collapse();
            app.Navigate(Catalog.TranslationNavigation, Catalog.Translation);
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Translation.Light");
        });
        app.Step("关闭", "正常退出", app.Close);
    }
}

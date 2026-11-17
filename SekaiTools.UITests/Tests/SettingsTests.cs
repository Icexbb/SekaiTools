using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
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
    public void UI_SETTINGS_002_开发者阈值校验保存重载与还原()
    {
        using var app = new ApplicationFixture("UI-SETTINGS-002", "Functional");
        const string field = "Settings.Threshold.DialogNametagNormal";
        var path = Path.Combine(app.Data.Paths.DataRoot, "Data", "developer-thresholds.json");
        app.Step("开启开发者设置", "版本标签键盘操作开启开发者模式并展开设置", () =>
        {
            app.Start();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            app.Finder.ById(app.Window!, "Settings.VersionLabel").Focus();
            for (var index = 0; index < 7; index++) Keyboard.Type(VirtualKeyShort.RETURN);
            var group = app.Finder.ById(app.Window!, "Settings.DeveloperGroup");
            group.Patterns.ExpandCollapse.Pattern.Expand();
            app.Finder.ById(app.Window!, field);
        });
        app.Step("校验与保存", "零阈值拒绝写盘，有效值仅在独立保存后持久化", () =>
        {
            var editor = app.Finder.ById(app.Window!, field).AsTextBox();
            editor.Text = "0";
            Keyboard.Type(VirtualKeyShort.TAB);
            app.Finder.ById(app.Window!, "Settings.Threshold.Save").AsButton().Invoke();
            WaitHelper.Until(() => app.Finder.ById(app.Window!, "Settings.Threshold.Status").Name.Contains("必须大于 0"), "非法阈值错误提示");
            Assert.False(File.Exists(path));
            editor.Text = "0.73";
            Keyboard.Type(VirtualKeyShort.TAB);
            Assert.False(File.Exists(path));
            app.Finder.ById(app.Window!, "Settings.Threshold.Save").AsButton().Invoke();
            WaitHelper.Until(() => File.Exists(path), "阈值文件保存");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(0.73, json.RootElement.GetProperty("DialogNametagNormal").GetDouble());
            var hash = SHA256.HashData(File.ReadAllBytes(path));
            editor.Text = "0";
            Keyboard.Type(VirtualKeyShort.TAB);
            app.Finder.ById(app.Window!, "Settings.Threshold.Save").AsButton().Invoke();
            WaitHelper.Until(() => app.Finder.ById(app.Window!, "Settings.Threshold.Status").Name.Contains("必须大于 0"), "拒绝覆盖已有阈值");
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
        });
        app.Step("重启还原", "重启恢复已保存值，恢复默认来源删除覆盖文件", () =>
        {
            app.Restart();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            app.Finder.ById(app.Window!, "Settings.DeveloperGroup").Patterns.ExpandCollapse.Pattern.Expand();
            Assert.Equal(0.73, double.Parse(app.Finder.ById(app.Window!, field).AsTextBox().Text, CultureInfo.InvariantCulture));
            app.Finder.ById(app.Window!, "Settings.Threshold.Reset").AsButton().Invoke();
            WaitHelper.Until(() => !File.Exists(path), "覆盖文件删除");
            WaitHelper.Until(() => app.Finder.ById(app.Window!, "Settings.Threshold.Status").Name.Contains("已恢复"), "恢复默认来源提示");
            Assert.NotEqual(0.73, double.Parse(app.Finder.ById(app.Window!, field).AsTextBox().Text, CultureInfo.InvariantCulture));
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Settings.Thresholds");
        });
        app.Step("退出", "开发者测试实例正常退出", app.Close);
    }

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
        app.Step("全局重置", "取消重置保留配置，确认重置恢复默认主题", () =>
        {
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            var choice = app.Finder.ById(app.Window!, theme).AsComboBox();
            choice.Select(1);
            choice.Collapse();
            var settings = Path.Combine(app.Data.Paths.DataRoot, "Data", "setting.json");
            var hash = SHA256.HashData(File.ReadAllBytes(settings));
            app.Finder.ById(app.Window!, "Settings.ResetAll").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("取消").And(cf.ByControlType(ControlType.Button))), "取消重置").AsButton().Invoke();
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(settings)));
            Assert.Equal("暗色", choice.SelectedItem?.Text);
            app.Finder.ById(app.Window!, "Settings.ResetAll").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("恢复默认").And(cf.ByControlType(ControlType.Button))), "确认重置").AsButton().Invoke();
            WaitHelper.Until(() => choice.SelectedItem?.Text == "亮色", "默认主题恢复");
        });
        app.Step("关闭", "正常退出", app.Close);
    }
}

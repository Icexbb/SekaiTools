using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
    public void UI_SETTINGS_005_受控更新响应并取消下载()
    {
        using var server = new LoopbackHttpFixture
        {
            Response = request => request.StartsWith("GET /latest ", StringComparison.Ordinal)
                ? """{"tag_name":"99.0.0.0"}"""
                : null
        };
        using var app = new ApplicationFixture("UI-SETTINGS-005", "Functional") { CheckUpdates = true };
        var endpoints = Path.Combine(app.Data.Root, "network-endpoints.json");
        File.WriteAllText(endpoints, File.ReadAllText(endpoints)
            .Replace("http://127.0.0.1:9/latest", $"http://127.0.0.1:{server.Port}/latest", StringComparison.Ordinal));
        app.Step("检查更新", "显示受控版本，取消后没有任何下载请求", () =>
        {
            app.Start();
            Assert.NotNull(WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("发现新版本 99.0.0.0")), "发现新版本"));
            var cancel = WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("暂不更新").And(cf.ByControlType(ControlType.Button))), "暂不更新");
            cancel.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("发现新版本 99.0.0.0")) is null, "更新提示关闭");
            Assert.StartsWith("GET /latest ", Assert.Single(server.Requests));
        });
        app.Step("关闭", "正常退出", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Visual")]
    public void UI_SETTINGS_004_最小窗口三主题设置布局()
    {
        using var app = new ApplicationFixture("UI-SETTINGS-004", "Visual");
        app.Step("三主题最小窗口", "分类顺序固定，主题编辑器与底栏完整可见", () =>
        {
            app.Start();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            var window = app.Window!;
            ResizeToMinimum(window);
            var theme = app.Finder.ById(window, "Settings.Field.CurrentApplicationTheme").AsComboBox();
            var proxy = app.Finder.ById(window, "Settings.Field.ProxyType").AsComboBox();
            proxy.Select(1);
            proxy.Collapse();
            var expected = new[] { "Settings.GeneralGroup", "Settings.RecognitionGroup", "Settings.DownloadGroup", "Settings.TranslationGroup", "Settings.About" };
            foreach (var (index, name) in new[] { (0, "Light"), (1, "Dark"), (2, "HighContrast") })
            {
                theme.Select(index);
                theme.Collapse();
                Assert.Equal(expected, window.FindAllDescendants()
                    .Select(element => element.Properties.AutomationId.ValueOrDefault)
                    .Where(expected.Contains));
                foreach (var element in new AutomationElement[]
                {
                    theme, app.Finder.ById(window, "Settings.Field.ProxyPort"),
                    app.Finder.ById(window, "Settings.ResetAll"), app.Finder.ById(window, "Settings.RuntimeLog")
                })
                {
                    Assert.False(element.IsOffscreen);
                    Assert.InRange(element.BoundingRectangle.Left, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                    Assert.InRange(element.BoundingRectangle.Right, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                }
                ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), $"Settings.{name}.Minimum");
            }
        });
        app.Step("关闭", "正常退出", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SETTINGS_003_动态编辑器保存重载与单项还原()
    {
        using var app = new ApplicationFixture("UI-SETTINGS-003", "Functional");
        AutomationElement Field(string key) => app.Finder.ById(app.Window!, $"Settings.Field.{key}");
        void Choice(string key, int index)
        {
            var choice = Field(key).AsComboBox();
            choice.Select(index);
            choice.Collapse();
        }

        void OpenGroups()
        {
            foreach (var key in new[] { "DialogFontFamily", "RecognitionThreadChoice" })
                app.Finder.ById(app.Window!, $"Settings.Section.{key}").Patterns.ExpandCollapse.Pattern.Expand();
        }

        var directory = Path.Combine(app.Data.Root, "SettingsDownload");
        Directory.CreateDirectory(directory);
        string originalMarker = "";
        bool? originalGray = null;
        string originalDirectory = "";
        string originalCharTime = "";
        string originalDownloadMode = "";
        app.Step("编辑", "通过 UIA 操作实际编辑器并自动保存", () =>
        {
            app.Start();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            OpenGroups();
            originalMarker = Field("TranslationNewlineMarker").AsComboBox().SelectedItem!.Text;
            originalGray = Field("RecognitionPrepareGray").AsCheckBox().IsChecked;
            originalDirectory = Field("DownloadDirectory").AsTextBox().Text;
            originalCharTime = Field("TypewriterCharTime").AsTextBox().Text;
            originalDownloadMode = Field("DownloadSourceMode").AsComboBox().SelectedItem!.Text;
            Choice("TranslationNewlineMarker", originalMarker == "↵" ? 0 : 2);
            Choice("DownloadSourceMode", 0);
            Choice("ProxyType", 1);
            Field("ProxyHost").AsTextBox().Text = "127.0.0.1";
            Field("ProxyPort").AsTextBox().Text = "43210";
            Field("TypewriterCharTime").AsTextBox().Text = "42";
            Field("DownloadDirectory").AsTextBox().Text = directory;
            Field("DialogFontFamily").AsTextBox().Text = "Segoe UI";
            Field("RecognitionPrepareGray").AsCheckBox().IsChecked = !originalGray;
            Assert.True(app.Finder.ById(app.Window!, "Settings.Choose.DownloadDirectory").Patterns.Invoke.IsSupported);
            Assert.True(app.Finder.ById(app.Window!, "Settings.Choose.DialogFontFamily").Patterns.Invoke.IsSupported);
        });
        app.Step("跨进程重载", "新进程读取修改值，单项还原仅复位所属设置", () =>
        {
            app.Restart();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            OpenGroups();
            Assert.Equal("http://", Field("ProxyType").AsComboBox().SelectedItem?.Text);
            Assert.Equal("127.0.0.1", Field("ProxyHost").AsTextBox().Text);
            Assert.Equal("43210", Field("ProxyPort").AsTextBox().Text);
            Assert.Equal("42", Field("TypewriterCharTime").AsTextBox().Text);
            Assert.Equal(directory, Field("DownloadDirectory").AsTextBox().Text);
            Assert.Equal("Segoe UI", Field("DialogFontFamily").AsTextBox().Text);
            Assert.True(app.Finder.ById(app.Window!, "Settings.Reset.DialogFontFamily").Patterns.Invoke.IsSupported);
            Assert.True(app.Finder.ById(app.Window!, "Settings.ResetSection.DialogFontFamily").Patterns.Invoke.IsSupported);
            Assert.Equal("手动选择下载源", Field("DownloadSourceMode").AsComboBox().SelectedItem?.Text);
            Assert.Equal(!originalGray, Field("RecognitionPrepareGray").AsCheckBox().IsChecked);
            Assert.NotEqual(originalMarker, Field("TranslationNewlineMarker").AsComboBox().SelectedItem?.Text);
            app.Finder.ById(app.Window!, "Settings.Reset.TranslationNewlineMarker").AsButton().Invoke();
            Assert.Equal(originalMarker, Field("TranslationNewlineMarker").AsComboBox().SelectedItem?.Text);
            Assert.Equal(directory, Field("DownloadDirectory").AsTextBox().Text);
        });
        app.Step("全局还原", "确认后下载、代理、轴机和实验设置均恢复", () =>
        {
            app.Finder.ById(app.Window!, "Settings.ResetAll").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("恢复默认").And(cf.ByControlType(ControlType.Button))), "确认恢复全局设置")
                .AsButton().Invoke();
            WaitHelper.Until(() => Field("ProxyType").AsComboBox().SelectedItem?.Text == "不使用", "代理恢复默认");
            Assert.Equal(originalDirectory, Field("DownloadDirectory").AsTextBox().Text);
            Assert.Equal(originalDownloadMode, Field("DownloadSourceMode").AsComboBox().SelectedItem?.Text);
            Assert.Equal(originalCharTime, Field("TypewriterCharTime").AsTextBox().Text);
            Assert.Equal(originalGray, Field("RecognitionPrepareGray").AsCheckBox().IsChecked);
        });
        app.Step("关于与日志", "展开关于卡片并打开独立日志窗口", () =>
        {
            app.Finder.ById(app.Window!, "Settings.About").Patterns.ExpandCollapse.Pattern.Expand();
            Assert.True(app.Finder.ById(app.Window!, "Settings.Repository").IsEnabled);
            app.Finder.ById(app.Window!, "Settings.RuntimeLog").AsButton().Invoke();
            app.OwnedWindow("RuntimeLog.Root").Close();
        });
        app.Step("关闭", "正常退出", app.Close);
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_SETTINGS_002_开发者阈值校验保存重载与还原()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var app = new ApplicationFixture("UI-SETTINGS-002", "Functional") { DeveloperPort = port };
        const string field = "Settings.Threshold.DialogNametagNormal";
        var path = Path.Combine(app.Data.Paths.DataRoot, "Data", "developer-thresholds.json");
        app.Step("开启开发者设置", "版本标签键盘操作开启开发者模式并展开设置", () =>
        {
            app.Start();
            app.Navigate(Catalog.SettingsNavigation, Catalog.Settings);
            Assert.Null(app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Settings.DeveloperGroup")));
            app.Finder.ById(app.Window!, "Settings.VersionLabel").Focus();
            for (var index = 0; index < 7; index++) Keyboard.Type(VirtualKeyShort.RETURN);
            var group = app.Finder.ById(app.Window!, "Settings.DeveloperGroup");
            group.Patterns.ExpandCollapse.Pattern.Expand();
            app.Finder.ById(app.Window!, field);
            var address = app.Finder.ById(app.Window!, "Settings.Developer.Address").AsTextBox().Text;
            Assert.Equal($"http://127.0.0.1:{port}/", address);
            Assert.Equal("••••••••", app.Finder.ById(app.Window!, "Settings.Developer.Token").AsTextBox().Text);
            Assert.True(app.Finder.ById(app.Window!, "Settings.Developer.CopyToken").IsEnabled);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(address), "api/v1/status"));
            using var response = client.Send(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        app.Step("关闭开发者模式", "隐藏分类并停止隔离端口监听", () =>
        {
            var window = app.Window!;
            ResizeToMinimum(window);
            var token = app.Finder.ById(window, "Settings.Developer.Token");
            token.Focus();
            Assert.False(token.IsOffscreen);
            Assert.InRange(token.BoundingRectangle.Right, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
            Assert.Equal(new[] { "Settings.TranslationGroup", "Settings.DeveloperGroup", "Settings.About" },
                window.FindAllDescendants().Select(element => element.Properties.AutomationId.ValueOrDefault)
                    .Where(id => id is "Settings.TranslationGroup" or "Settings.DeveloperGroup" or "Settings.About"));
            ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), "Settings.Developer.Minimum");
            app.Finder.ById(app.Window!, "Settings.Developer.Disable").AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Settings.DeveloperGroup")) is null, "隐藏开发者分类");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/");
            Assert.Throws<HttpRequestException>(() => client.Send(request));
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

    private static void ResizeToMinimum(Window window)
    {
        var dpi = GetDpiForWindow(window.Properties.NativeWindowHandle.Value);
        var width = (int)Math.Round(960 * dpi / 96d);
        var height = (int)Math.Round(600 * dpi / 96d);
        var desktop = window.Automation.GetDesktop().BoundingRectangle;
        if (dpi == 0 || desktop.Width < width || desktop.Height < height)
            throw new InvalidOperationException("当前桌面无法容纳 960×600 DIP 的设置窗口。");
        var transform = window.Patterns.Transform.Pattern;
        transform.Resize(width, height);
        transform.Move(desktop.Left + (desktop.Width - width) / 2, desktop.Top + (desktop.Height - height) / 2);
        WaitHelper.Until(() => Math.Abs(window.BoundingRectangle.Width - width) <= 1 &&
                               Math.Abs(window.BoundingRectangle.Height - height) <= 1, "960×600 DIP 窗口");
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}

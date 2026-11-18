using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchShowcaseTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_001_公共组件三种应用主题交互与状态保持()
    {
        using var app = new ApplicationFixture("UI-DESIGN-001", "Functional");
        app.Step("启动", "隔离应用就绪", app.Start);
        using (Keyboard.Pressing(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT)) Keyboard.Type(VirtualKeyShort.F12);
        var showcase = WaitHelper.Until(() => app.Window!.ModalWindows.FirstOrDefault(window => window.Properties.AutomationId.ValueOrDefault == "Showcase.Root"), "组件预览窗口");
        app.Step("三主题交互", "编辑与选择保持，禁用和校验状态可操作并截图", () =>
        {
            try
            {
                var editor = app.Finder.ById(showcase, "Showcase.Editor").AsTextBox();
                Assert.Equal("多行译文样例", editor.Name);
                editor.Text = "示例译文保持。";
                var selection = app.Finder.ById(showcase, "Showcase.Selection").AsCheckBox();
                selection.IsChecked = false;
                var theme = app.Finder.ById(showcase, "Showcase.Theme").AsComboBox();
                Assert.Equal("预览主题", theme.Name);
                var translation = app.Finder.ById(showcase, "Translation.Dialog.1.TargetText").AsTextBox();
                translation.Text = "真实翻译行编辑。";
                foreach (var (index, name) in new[] { (0, "Light"), (1, "Dark"), (2, "HighContrast") })
                {
                    theme.Select(index);
                    theme.Collapse();
                    Assert.Equal("示例译文保持。", editor.Text);
                    Assert.Equal("真实翻译行编辑。", translation.Text);
                    Assert.False(selection.IsChecked);
                    Assert.False(app.Finder.ById(showcase, "Showcase.Disabled").IsEnabled);
                    var input = app.Finder.ById(showcase, "Showcase.Threshold").AsTextBox();
                    Assert.Equal("识别阈值样例", input.Name);
                    input.Text = "0";
                    var confirm = app.Finder.ById(showcase, "Showcase.Confirm");
                    WaitHelper.Until(() => !confirm.IsEnabled, "非法输入禁止确认");
                    var notice = app.Finder.ById(showcase, "Showcase.Validation");
                    Assert.Contains("请输入大于0且不超过1", notice.Name);
                    Assert.False(notice.IsOffscreen);
                    ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), name + ".Invalid");
                    input.Text = "0.75";
                    WaitHelper.Until(() => confirm.IsEnabled, "有效输入恢复确认");
                    confirm.AsButton().Invoke();
                    WaitHelper.Until(() => app.Finder.ById(showcase, "Showcase.Result").Name.Contains("示例已确认"), "真实按钮交互反馈");
                    editor.Focus();
                    Assert.True(editor.Properties.HasKeyboardFocus.Value);
                    var screenshot = ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), name + ".Focused");
                    using var bitmap = new System.Drawing.Bitmap(screenshot);
                    var background = bitmap.GetPixel(10, 100);
                    if (index == 1) Assert.True(background.R < 60 && background.G < 60 && background.B < 60, "暗色内容根背景必须实际变暗。");
                    if (index == 0) Assert.True(background.R > 200 && background.G > 200 && background.B > 200, "亮色内容根背景必须保持亮色。");
                }
            }
            catch (Exception exception)
            {
                FailureDiagnostics.Save(app.Data, showcase, app.Finder.TargetId, exception, "ShowcaseFailure");
                throw;
            }
        });
        app.Step("紧凑预览", "800×600下主操作可见，公共工具栏真实折行", () =>
        {
            showcase.Patterns.Transform.Pattern.Resize(800, 600);
            WaitHelper.Until(() => showcase.BoundingRectangle.Width <= 810, "预览窗口缩小");
            var first = app.Finder.ById(showcase, "Showcase.Toolbar.First");
            var second = app.Finder.ById(showcase, "Showcase.Toolbar.Second");
            Assert.True(second.BoundingRectangle.Top >= first.BoundingRectangle.Bottom);
            var editor = app.Finder.ById(showcase, "Showcase.Editor");
            editor.Focus();
            Assert.False(editor.IsOffscreen);
            Assert.False(app.Finder.ById(showcase, "Showcase.Confirm").IsOffscreen);
            ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), "Showcase.Narrow");
        });
        app.Step("关闭预览", "关闭后返回原工作区", () =>
        {
            showcase.Close();
            WaitHelper.Until(() => app.Window!.ModalWindows.Length == 0, "预览窗口关闭");
            app.Navigate("MainWindow.Navigation.Settings", "Settings.Root");
            Assert.Equal("亮色", app.Finder.ById(app.Window!, "Settings.Field.CurrentApplicationTheme").AsComboBox().SelectedItem?.Text);
            app.Navigate("MainWindow.Navigation.Translation", "Translation.Root");
            app.Finder.ById(app.Window!, "Translation.LoadScriptButton");
        });
        app.Step("退出", "隔离应用正常退出", app.Close);
    }
}

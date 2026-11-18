using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchSplitTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_006_分栏拖动键盘与紧凑恢复()
    {
        using var app = new ApplicationFixture("UI-DESIGN-006", "Functional");
        app.Step("启动", "隔离应用就绪", app.Start);
        Window showcase = null!;
        app.Step("打开预览", "真实入口打开分栏样例", () =>
        {
            app.Window!.SetForeground();
            app.Finder.ById(app.Window, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT)) Keyboard.Type(VirtualKeyShort.F12);
            showcase = app.OwnedWindow(Catalog.Showcase);
        });
        var cases = new List<object>();
        app.Step("分栏三主题", "鼠标和键盘调整宽度，窄窗口保留编辑并收起辅助区，恢复后内容与宽度不丢失", () =>
        {
            try
            {
                Resize(1280, 960);
                var editor = app.Finder.ById(showcase, "Showcase.Split.Editor").AsTextBox();
                var reference = app.Finder.ById(showcase, "Showcase.Split.Reference").AsTextBox();
                editor.Text = "分栏译文测试。";
                reference.Text = "保留参考内容。";
                foreach (var (text, themeName) in new[] { ("亮色", "Light"), ("暗色", "Dark"), ("高对比", "HighContrast") })
                {
                    var theme = app.Finder.ById(showcase, "Showcase.Theme").AsComboBox();
                    var scroll = app.Finder.ById(showcase, Catalog.ShowcaseScroll).Patterns.Scroll.Pattern;
                    if (scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 0);
                    theme.Focus();
                    theme.Expand();
                    WaitHelper.Until(() => theme.Items.FirstOrDefault(item => item.Text == text), "主题选项就绪")
                        .Patterns.SelectionItem.Pattern.Select();
                    theme.Collapse();
                    WaitHelper.Until(() => theme.SelectedItem?.Text == text, "分栏主题已切换");
                    editor.Focus();
                    Assert.False(reference.IsOffscreen);
                    var splitter = app.Finder.ById(showcase, "Workbench.Splitter");
                    var before = editor.BoundingRectangle.Width;
                    splitter.Focus();
                    Assert.True(splitter.Properties.HasKeyboardFocus.Value);
                    Keyboard.Type(VirtualKeyShort.RIGHT);
                    WaitHelper.Until(() => editor.BoundingRectangle.Width > before + 8, "方向键调整分栏");
                    var afterKeyboard = editor.BoundingRectangle.Width;
                    var bounds = splitter.BoundingRectangle;
                    Mouse.DragHorizontally(new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)), 32, MouseButton.Left);
                    WaitHelper.Until(() => editor.BoundingRectangle.Width > afterKeyboard + 8, "鼠标拖动调整分栏");
                    var adjusted = editor.BoundingRectangle.Width;
                    Assert.True(reference.BoundingRectangle.Width >= 224, "辅助区应保留最小宽度。");
                    splitter.Focus();
                    ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), themeName + ".SplitFocused");
                    reference.Focus();
                    Resize(800, 600);
                    WaitHelper.Until(() => reference.IsOffscreen, "紧凑窗口收起辅助区");
                    Assert.True(splitter.IsOffscreen);
                    Assert.True(editor.Properties.HasKeyboardFocus.Value, "辅助区收起时焦点应回到主编辑区。");
                    Assert.False(editor.IsOffscreen);
                    WaitHelper.Until(() => editor.BoundingRectangle.Bottom <= showcase.BoundingRectangle.Bottom - 4, "紧凑主编辑器完整位于视口内");
                    Assert.Equal("分栏译文测试。", editor.Text);
                    ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), themeName + ".SplitCompact");
                    Resize(1280, 960);
                    editor.Focus();
                    WaitHelper.Until(() => !reference.IsOffscreen && !splitter.IsOffscreen, "恢复辅助区");
                    Assert.Equal("保留参考内容。", reference.Text);
                    Assert.Equal("分栏译文测试。", editor.Text);
                    Assert.InRange(editor.BoundingRectangle.Width, adjusted - 1, adjusted + 1);
                    ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), themeName + ".SplitRestored");
                    cases.Add(new { Theme = themeName, Before = before, AfterKeyboard = afterKeyboard, AfterDrag = adjusted, Restored = editor.BoundingRectangle.Width, Result = "Passed" });
                }
            }
            catch (Exception exception)
            {
                FailureDiagnostics.Save(app.Data, showcase, app.Finder.TargetId, exception, "ShowcaseFailure");
                throw;
            }
            finally { app.Data.WriteJson("split-matrix", cases); }
        });
        app.Step("退出", "预览和主窗口正常关闭", () => { showcase.Close(); app.Close(); });

        void Resize(int width, int height)
        {
            var desktop = showcase.Automation.GetDesktop().BoundingRectangle;
            if (desktop.Width < width + 24 || desktop.Height < height + 24)
                throw new InvalidOperationException($"分栏验证环境不满足：屏幕不足以容纳 {width}×{height}。");
            showcase.Patterns.Transform.Pattern.Resize(width, height);
            showcase.Patterns.Transform.Pattern.Move(desktop.Left + (desktop.Width - width) / 2, desktop.Top + (desktop.Height - height) / 2);
            WaitHelper.Until(() => Math.Abs(showcase.BoundingRectangle.Width - width) <= 1 && Math.Abs(showcase.BoundingRectangle.Height - height) <= 1, "实际分栏窗口尺寸");
        }
    }
}

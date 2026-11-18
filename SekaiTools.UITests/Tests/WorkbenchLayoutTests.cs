using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchLayoutTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_003_长标题与三主题四尺寸组合()
    {
        using var app = new ApplicationFixture("UI-DESIGN-003", "Functional");
        app.Step("启动", "隔离应用就绪", app.Start);
        Window showcase = null!;
        app.Step("打开预览", "真实快捷键打开自有组件预览窗口", () =>
        {
            app.Window!.SetForeground();
            app.Finder.ById(app.Window, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT)) Keyboard.Type(VirtualKeyShort.F12);
            showcase = app.OwnedWindow(Catalog.Showcase);
        });
        var cases = new List<object>();
        app.Step("主题尺寸矩阵", "长标题完整换行，滚动后编辑、选择与校验可达", () =>
        {
            try
            {
                app.Finder.ById(showcase, Catalog.ShowcaseLongTitle).AsCheckBox().IsChecked = true;
                var theme = app.Finder.ById(showcase, "Showcase.Theme").AsComboBox();
                var scroll = app.Finder.ById(showcase, Catalog.ShowcaseScroll).Patterns.Scroll.Pattern;
                var editor = app.Finder.ById(showcase, "Showcase.Editor").AsTextBox();
                editor.Text = "矩阵检查译文。";
                var selection = app.Finder.ById(showcase, "Showcase.Selection").AsCheckBox();
                selection.IsChecked = false;
                var desktop = showcase.Automation.GetDesktop().BoundingRectangle;
                foreach (var (width, height) in new[] { (800, 600), (1280, 720), (1280, 960), (1920, 1080) })
                {
                    if (desktop.Width < width + 24 || desktop.Height < height + 24)
                        throw new InvalidOperationException($"布局验证环境不满足：屏幕不足以容纳 {width}×{height}。");
                    showcase.Patterns.Transform.Pattern.Resize(width, height);
                    showcase.Patterns.Transform.Pattern.Move(desktop.Left + (desktop.Width - width) / 2, desktop.Top + (desktop.Height - height) / 2);
                    WaitHelper.Until(() => Math.Abs(showcase.BoundingRectangle.Width - width) <= 1 && Math.Abs(showcase.BoundingRectangle.Height - height) <= 1, "目标窗口尺寸");
                    ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), $"Resize.{width}x{height}");
                    foreach (var (index, name) in new[] { (0, "Light"), (1, "Dark"), (2, "HighContrast") })
                    {
                        if (scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 0);
                        showcase.SetForeground();
                        theme.Focus();
                        theme.Expand();
                        var expectedTheme = new[] { "亮色", "暗色", "高对比" }[index];
                        var option = WaitHelper.Until(() => theme.Items.FirstOrDefault(item => item.Text == expectedTheme), "目标主题选项就绪");
                        option.Patterns.SelectionItem.Pattern.Select();
                        theme.Collapse();
                        WaitHelper.Until(() => theme.SelectedItem?.Text == expectedTheme, "目标主题已选中");
                        var title = app.Finder.ById(showcase, "Workbench.Header.Title");
                        Assert.StartsWith("Fluent Workbench · 长标题压力验证", title.Name);
                        Assert.EndsWith("完整换行并保持操作可达。", title.Name);
                        Assert.True(title.BoundingRectangle.Height > 40, "长标题应实际换行。");
                        Assert.InRange(title.BoundingRectangle.Left, showcase.BoundingRectangle.Left, showcase.BoundingRectangle.Right);
                        Assert.InRange(title.BoundingRectangle.Right, showcase.BoundingRectangle.Left, showcase.BoundingRectangle.Right);
                        var titleBounds = title.BoundingRectangle.ToString();
                        var prefix = $"{name}.{width}x{height}";
                        var headerScreenshot = ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".Header");
                        using (var bitmap = new System.Drawing.Bitmap(headerScreenshot))
                        {
                            var background = bitmap.GetPixel(10, 100);
                            var left = (int)(title.BoundingRectangle.Left - showcase.BoundingRectangle.Left) - 36;
                            var top = (int)(title.BoundingRectangle.Top - showcase.BoundingRectangle.Top);
                            var visibleIconPixels = 0;
                            for (var x = left; x < left + 24; x++)
                                for (var y = top; y < top + 32; y++)
                                {
                                    var pixel = bitmap.GetPixel(x, y);
                                    if (Math.Abs(pixel.R - background.R) + Math.Abs(pixel.G - background.G) + Math.Abs(pixel.B - background.B) > 180) visibleIconPixels++;
                                }
                            Assert.True(visibleIconPixels > 5, "页头图标应在标题首行旁可见，不能落到长标题中央。");
                        }
                        Assert.Equal("矩阵检查译文。", editor.Text);
                        Assert.False(selection.IsChecked);
                        app.Finder.ById(showcase, "Showcase.Threshold").AsTextBox().Text = "0";
                        selection.Focus();
                        var notice = app.Finder.ById(showcase, "Showcase.Validation");
                        Assert.False(notice.IsOffscreen);
                        Assert.InRange(notice.BoundingRectangle.Bottom, showcase.BoundingRectangle.Top, showcase.BoundingRectangle.Bottom);
                        var noticeBounds = notice.BoundingRectangle.ToString();
                        var confirm = app.Finder.ById(showcase, "Showcase.Confirm");
                        Assert.False(confirm.IsEnabled);
                        ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".Invalid");
                        app.Finder.ById(showcase, "Showcase.Threshold").AsTextBox().Text = "0.8";
                        WaitHelper.Until(() => confirm.IsEnabled, "恢复有效输入");
                        confirm.AsButton().Invoke();
                        editor.Focus();
                        Assert.True(editor.Properties.HasKeyboardFocus.Value);
                        ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), prefix + ".Focused");
                        cases.Add(new { Theme = name, Width = width, Height = height, Title = titleBounds, Notice = noticeBounds, Result = "Passed" });
                    }
                }
            }
            catch (Exception exception)
            {
                FailureDiagnostics.Save(app.Data, showcase, app.Finder.TargetId, exception, "ShowcaseFailure");
                throw;
            }
            finally { app.Data.WriteJson("layout-matrix", cases); }
        });
        app.Step("退出", "预览与主窗口正常关闭", () => { showcase.Close(); app.Close(); });
    }
}

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchStateTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_DESIGN_004_任务六状态三主题与实际文字对比度()
    {
        using var app = new ApplicationFixture("UI-DESIGN-004", "Functional");
        app.Step("启动", "隔离工作区就绪", app.Start);
        Window showcase = null!;
        app.Step("打开预览", "实际预览入口可用", () =>
        {
            app.Window!.SetForeground();
            app.Finder.ById(app.Window, Catalog.LoadScript).Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT)) Keyboard.Type(VirtualKeyShort.F12);
            showcase = app.OwnedWindow(Catalog.Showcase);
        });
        var results = new List<object>();
        app.Step("任务状态矩阵", "六种真实任务状态在三主题均可读，错误可清除，预览不执行任务", () =>
        {
            try
            {
                var scroll = app.Finder.ById(showcase, Catalog.ShowcaseScroll).Patterns.Scroll.Pattern;
                var theme = app.Finder.ById(showcase, "Showcase.Theme").AsComboBox();
                var state = app.Finder.ById(showcase, "Showcase.TaskState").AsComboBox();
                foreach (var (themeText, name) in new[] { ("亮色", "Light"), ("暗色", "Dark"), ("高对比", "HighContrast") })
                {
                    if (scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 0);
                    Select(theme, themeText);
                    foreach (var (stateText, stateName) in new[] { ("等待下载", "Waiting"), ("下载中", "Downloading"),
                        ("下载完成", "Completed"), ("下载失败", "Failed"), ("已停止", "Stopped"), ("文件已存在，已跳过", "Skipped") })
                    {
                        if (scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 100);
                        Select(state, stateText);
                        if (scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 100);
                        var status = app.Finder.ById(showcase, "StoryDownload.Task.Status");
                        WaitHelper.Until(() => status.Name == stateText, "真实任务状态更新");
                        Assert.False(status.IsOffscreen);
                        var error = showcase.FindFirstDescendant(cf => cf.ByAutomationId("StoryDownload.Task.Error"));
                        if (stateName == "Failed")
                        {
                            Assert.NotNull(error);
                            Assert.Contains("连接失败", error!.Name);
                            Assert.False(error.IsOffscreen);
                        }
                        else Assert.True(error is null || error.IsOffscreen || string.IsNullOrEmpty(error.Name), "离开失败状态后不应残留错误文本。");
                        if (stateName is "Failed" or "Stopped")
                            Assert.False(app.Finder.ById(showcase, "StoryDownload.Task.RetryButton").IsEnabled);
                        var screenshot = ScreenshotHelper.CaptureStable(showcase, Path.Combine(app.Data.Directory, "Screenshots"), name + "." + stateName);
                        var bounds = status.BoundingRectangle;
                        using var bitmap = new Bitmap(screenshot);
                        var left = (int)(bounds.Left - showcase.BoundingRectangle.Left);
                        var top = (int)(bounds.Top - showcase.BoundingRectangle.Top);
                        var background = bitmap.GetPixel(left - 2, top + (int)bounds.Height / 2);
                        var maximum = 1d;
                        for (var x = left; x < left + (int)bounds.Width; x++)
                            for (var y = top; y < top + (int)bounds.Height; y++)
                                maximum = Math.Max(maximum, Contrast(bitmap.GetPixel(x, y), background));
                        results.Add(new { Theme = name, State = stateName, Text = status.Name, MaxInkContrast = maximum, Background = background.ToArgb().ToString("X8") });
                        Assert.True(maximum >= 4.5, $"{name}/{stateName} 状态文字的实际前景对比度不足：{maximum:F2}:1。");
                    }
                }
            }
            catch (Exception exception)
            {
                FailureDiagnostics.Save(app.Data, showcase, app.Finder.TargetId, exception, "ShowcaseFailure");
                throw;
            }
            finally { app.Data.WriteJson("task-state-matrix", results); }
        });
        app.Step("退出", "预览与主窗口正常关闭", () => { showcase.Close(); app.Close(); });
    }

    private static void Select(ComboBox combo, string text)
    {
        combo.Focus();
        combo.Expand();
        var option = WaitHelper.Until(() => combo.Items.FirstOrDefault(item => item.Text == text), "真实选项就绪：" + text);
        option.Patterns.SelectionItem.Pattern.Select();
        combo.Collapse();
        WaitHelper.Until(() => combo.SelectedItem?.Text == text, "选项已选中：" + text);
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte value) { var v = value / 255d; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}

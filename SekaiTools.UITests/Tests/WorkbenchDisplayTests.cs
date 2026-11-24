using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class WorkbenchDisplayTests
{
    [DesktopFact]
    [Trait("Category", "Visual")]
    public void UI_DISPLAY_001_真实桌面DPI下的五个页面()
    {
        using var app = new ApplicationFixture("UI-DISPLAY-001", "Visual");
        using var resources = new LoopbackHttpFixture();
        ResourcePackFixture.ServeWorkbench(app, resources);
        app.Step("启动", "读取真实窗口 DPI 与系统高对比状态", app.Start);
        var window = app.Window!;
        var handle = window.Properties.NativeWindowHandle.Value;
        var dpi = GetDpiForWindow(handle);
        Assert.True(dpi > 0);
        var systemHighContrast = System.Windows.SystemParameters.HighContrast;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        Assert.True(GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor));
        var workArea = System.Drawing.Rectangle.FromLTRB(monitor.WorkArea.Left, monitor.WorkArea.Top, monitor.WorkArea.Right, monitor.WorkArea.Bottom);
        app.Data.WriteJson("display", new { Dpi = dpi, SystemHighContrast = systemHighContrast, WorkArea = workArea.ToString() });
        var pages = new[]
        {
            (Catalog.TranslationNavigation, Catalog.Translation, Catalog.LoadScript),
            (Catalog.DownloadNavigation, "StoryDownload.Root", "StoryDownload.RefreshButton"),
            (Catalog.SubtitleNavigation, "Subtitle.Root", "Subtitle.AddTaskButton"),
            (Catalog.EncodingNavigation, "Encoding.Root", "Encoding.AddTaskButton"),
            (Catalog.SettingsNavigation, Catalog.Settings, "Settings.ResetAll")
        };
        var captures = new List<object>();
        try
        {
            foreach (var (width, height) in new[] { (960, 600), (1280, 720), (1280, 960), (1920, 1080) })
            {
                var pixelWidth = (int)Math.Round(width * dpi / 96d);
                var pixelHeight = (int)Math.Round(height * dpi / 96d);
                if (workArea.Width < pixelWidth || workArea.Height < pixelHeight)
                {
                    captures.Add(new { Width = width, Height = height, Result = "InsufficientDesktopSize" });
                    Assert.NotEqual(960, width);
                    continue;
                }
                app.Step($"{width}×{height} DIP", "五个页面根与主要操作可见，保存真实像素截图", () =>
                {
                    var transform = window.Patterns.Transform.Pattern;
                    transform.Resize(pixelWidth, pixelHeight);
                    transform.Move(workArea.Left + (workArea.Width - pixelWidth) / 2, workArea.Top + (workArea.Height - pixelHeight) / 2);
                    WaitHelper.Until(() => Math.Abs(window.BoundingRectangle.Width - pixelWidth) <= 1 &&
                                           Math.Abs(window.BoundingRectangle.Height - pixelHeight) <= 1, "目标窗口尺寸");
                    foreach (var (navigation, page, command) in pages)
                    {
                        app.Navigate(navigation, page);
                        var action = app.Finder.ById(window, command);
                        if (page is "Subtitle.Root" or "Encoding.Root")
                            WaitHelper.Until(() => action.IsEnabled, $"{page} 资源就绪", TimeSpan.FromSeconds(50));
                        Assert.False(action.IsOffscreen);
                        var bounds = action.BoundingRectangle;
                        Assert.True(bounds.Width > 0 && bounds.Height > 0);
                        Assert.InRange(bounds.Left, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                        Assert.InRange(bounds.Right, window.BoundingRectangle.Left, window.BoundingRectangle.Right);
                        Assert.InRange(bounds.Top, window.BoundingRectangle.Top, window.BoundingRectangle.Bottom);
                        Assert.InRange(bounds.Bottom, window.BoundingRectangle.Top, window.BoundingRectangle.Bottom);
                        var capture = ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"),
                            $"{page}.{width}x{height}.{dpi}dpi");
                        captures.Add(new { Page = page, Width = width, Height = height, PixelWidth = pixelWidth, PixelHeight = pixelHeight, Capture = Path.GetFileName(capture) });
                    }
                });
            }
        }
        finally { app.Data.WriteJson("display-captures", captures); }
        app.Step("关闭", "正常退出隔离实例", app.Close);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRectangle Monitor;
        public NativeRectangle WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

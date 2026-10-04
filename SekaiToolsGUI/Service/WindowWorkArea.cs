using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SekaiToolsGUI.Service;

/// <summary>Keeps windows within their current monitor's usable area, in WPF units.</summary>
internal sealed class WindowWorkArea
{
    private readonly Window _window;
    private readonly double _minWidth;
    private readonly double _minHeight;
    private nint _monitor;
    private bool _pending;

    private WindowWorkArea(Window window)
    {
        _window = window;
        _minWidth = window.MinWidth;
        _minHeight = window.MinHeight;
        window.SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(WindowProc);
            Fit(true);
            QueueFit(); // Recheck after WPF applies startup placement and the monitor's DPI.
        };
        window.LocationChanged += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != 0 && MonitorFromWindow(handle, 2) != _monitor) QueueFit();
        };
        window.StateChanged += (_, _) => QueueFit();
    }

    public static void Attach(Window window) => _ = new WindowWorkArea(window);

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // DPI, display configuration, and taskbar/work-area changes.
        if (message is 0x02E0 or 0x007E or 0x001A) QueueFit();
        return 0;
    }

    private void QueueFit()
    {
        if (_pending) return;
        _pending = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _pending = false;
            Fit(false);
        }));
    }

    private void Fit(bool initial)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        _monitor = MonitorFromWindow(handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(_monitor, ref info)) return;
        var dpi = GetDpiForWindow(handle);
        var scale = dpi == 0 ? 1 : dpi / 96d;
        var area = new Rect(info.Work.Left / scale, info.Work.Top / scale,
            (info.Work.Right - info.Work.Left) / scale, (info.Work.Bottom - info.Work.Top) / scale);
        var width = Math.Max(1, area.Width - 24);
        var height = Math.Max(1, area.Height - 24);
        _window.MinWidth = Math.Min(_minWidth, width);
        _window.MinHeight = Math.Min(_minHeight, height);
        if (_window.WindowState != WindowState.Normal) return;
        _window.Width = Math.Clamp(_window.Width, _window.MinWidth, width);
        _window.Height = Math.Clamp(_window.Height, _window.MinHeight, height);
        var left = double.IsFinite(_window.Left) ? _window.Left : area.Left;
        var top = double.IsFinite(_window.Top) ? _window.Top : area.Top;
        if (initial && _window.WindowStartupLocation != WindowStartupLocation.Manual)
        {
            var owner = _window.WindowStartupLocation == WindowStartupLocation.CenterOwner ? _window.Owner : null;
            left = owner != null && double.IsFinite(owner.Left)
                ? owner.Left + (owner.ActualWidth - _window.Width) / 2
                : area.Left + (area.Width - _window.Width) / 2;
            top = owner != null && double.IsFinite(owner.Top)
                ? owner.Top + (owner.ActualHeight - _window.Height) / 2
                : area.Top + (area.Height - _window.Height) / 2;
        }
        _window.Left = Math.Clamp(left, area.Left, area.Right - _window.Width);
        _window.Top = Math.Clamp(top, area.Top, area.Bottom - _window.Height);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}

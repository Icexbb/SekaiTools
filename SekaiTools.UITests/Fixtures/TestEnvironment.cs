using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SekaiTools.UITests.Fixtures;

public static class TestEnvironment
{
    public static object Verify()
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || Process.GetCurrentProcess().SessionId == 0)
            throw new InvalidOperationException("UI 测试环境不满足：需要交互式 Windows 用户会话。");
        var desktop = OpenInputDesktop(0, false, 1);
        if (desktop == IntPtr.Zero)
            throw new InvalidOperationException("UI 测试环境不满足：无法访问输入桌面，可能已锁屏或权限不足。");
        string desktopName;
        try
        {
            var name = new StringBuilder(256);
            if (!GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out _))
                throw new InvalidOperationException("无法读取输入桌面名称。");
            desktopName = name.ToString();
            if (!desktopName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"UI 测试环境不满足：当前输入桌面为 {desktopName}。");
        }
        finally { CloseDesktop(desktop); }

        if (Mutex.TryOpenExisting(@"Local\SekaiToolsGUI-1D56E931-7BB9-4E91-B960-76A04EC83C45", out var mutex))
        {
            mutex.Dispose();
            throw new InvalidOperationException("UI 测试环境冲突：已有 SekaiTools 实例。请结束正式实例后重试；测试不会附加或关闭它。");
        }

        return new { SessionId = Process.GetCurrentProcess().SessionId, Desktop = desktopName,
            OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(),
            ScreenWidth = GetSystemMetrics(0), ScreenHeight = GetSystemMetrics(1), SystemDpi = GetDpiForSystem() };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder information, int length, out int needed);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}

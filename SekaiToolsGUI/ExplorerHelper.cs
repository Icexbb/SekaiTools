using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace SekaiToolsGUI;

public static class ExplorerHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void OpenFolderAndFocus(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show("文件不存在或路径错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var targetFolder = Path.GetDirectoryName(filePath);
        var found = false;

        var desktop = AutomationElement.RootElement;
        var explorerWindows = desktop.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, "CabinetWClass"));

        foreach (AutomationElement window in explorerWindows)
        {
            var docElement = window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

            if (docElement == null) continue;
            if (docElement.GetCurrentPattern(ValuePattern.Pattern) is not ValuePattern pattern) continue;
            var currentPath = pattern.Current.Value;

            // 注意：路径可能是带盘符的全路径
            if (!string.Equals(currentPath, targetFolder, StringComparison.OrdinalIgnoreCase)) continue;
            // 找到匹配窗口，设置为前台
            var hwnd = new IntPtr(window.Current.NativeWindowHandle);
            SetForegroundWindow(hwnd);
            found = true;
            break;
        }

        if (!found)
            // 没找到，就打开并选中
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
    }
}
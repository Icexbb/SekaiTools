using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using SekaiToolsBase;

namespace SekaiToolsGUI.Service;

public static class WindowsNotificationService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            ToastNotificationManagerCompat.OnActivated += OnNotificationInvoked;
            _initialized = true;
        }
        catch (Exception e)
        {
            Logger.Log($"注册 Windows 通知失败: {e}", LogLevel.Warning);
        }
    }

    public static void Show(string title, string message)
    {
        if (!_initialized) return;

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch (Exception e)
        {
            Logger.Log($"显示 Windows 通知失败: {e.Message}", LogLevel.Warning);
        }
    }

    public static void Shutdown()
    {
        if (!_initialized) return;

        try
        {
            ToastNotificationManagerCompat.OnActivated -= OnNotificationInvoked;
        }
        catch (Exception e)
        {
            Logger.Log($"注销 Windows 通知失败: {e.Message}", LogLevel.Warning);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void OnNotificationInvoked(ToastNotificationActivatedEventArgsCompat args)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (Application.Current.MainWindow is not MainWindow window) return;

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Show();
            window.Activate();
        });
    }
}

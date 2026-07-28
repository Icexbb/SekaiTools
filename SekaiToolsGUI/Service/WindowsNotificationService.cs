using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SekaiToolsBase;

namespace SekaiToolsGUI.Service;

public static class WindowsNotificationService
{
    private static AppNotificationManager? _manager;
    private static bool _registered;

    public static void Initialize()
    {
        if (_registered) return;

        try
        {
            _manager = AppNotificationManager.Default;
            _manager.NotificationInvoked += OnNotificationInvoked;
            _manager.Register();
            _registered = true;
        }
        catch (Exception e)
        {
            if (_manager != null)
                _manager.NotificationInvoked -= OnNotificationInvoked;
            _manager = null;
            Logger.Log($"注册 Windows 通知失败: {e}", LogLevel.Warning);
        }
    }

    public static void Show(string title, string message)
    {
        if (!_registered || _manager == null) return;

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            _manager.Show(notification);
        }
        catch (Exception e)
        {
            Logger.Log($"显示 Windows 通知失败: {e.Message}", LogLevel.Warning);
        }
    }

    public static void Shutdown()
    {
        if (!_registered || _manager == null) return;

        try
        {
            _manager.NotificationInvoked -= OnNotificationInvoked;
            _manager.Unregister();
        }
        catch (Exception e)
        {
            Logger.Log($"注销 Windows 通知失败: {e.Message}", LogLevel.Warning);
        }
        finally
        {
            _registered = false;
            _manager = null;
        }
    }

    private static void OnNotificationInvoked(AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
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

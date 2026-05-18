using System.Windows;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiToolsGUI;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        InitializeComponent();
        Logger.Log("SekaiToolsGUI 启动", LogLevel.Information);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Log($"SekaiToolsGUI 退出 (exitCode={e.ApplicationExitCode})", LogLevel.Information);
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Log($"UI线程未处理异常: {e.Exception.Message}\n{e.Exception.StackTrace}", LogLevel.Critical);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Log($"未处理异常(IsTerminating={e.IsTerminating}): {ex.Message}\n{ex.StackTrace}", LogLevel.Critical);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Log($"未观察任务异常: {e.Exception.Message}\n{e.Exception.StackTrace}", LogLevel.Error);
    }


}
using System.Runtime.ExceptionServices;
using Avalonia;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiToolsAvalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        Logger.Log("SekaiToolsAvalonia 启动", LogLevel.Information);

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Log($"应用崩溃: {ex.Message}\n{ex.StackTrace}", LogLevel.Critical);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Log($"未处理异常(IsTerminating={e.IsTerminating}): {ex.Message}\n{ex.StackTrace}",
                LogLevel.Critical);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Log($"未观察任务异常: {e.Exception.Message}\n{e.Exception.StackTrace}", LogLevel.Error);
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        Logger.Log($"首次机会异常: {e.Exception.Message}\n{e.Exception.StackTrace}", LogLevel.Trace);
    }
}

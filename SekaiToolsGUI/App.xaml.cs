using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Service;

namespace SekaiToolsGUI;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\SekaiToolsGUI-1D56E931-7BB9-4E91-B960-76A04EC83C45";
    private bool _ownsSingleInstanceMutex;
    private Mutex? _singleInstanceMutex;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        InitializeComponent();
        Logger.Log("SekaiToolsGUI 启动");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show("SekaiTools 已在运行中。", "SekaiTools",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        WindowsNotificationService.Initialize();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WindowsNotificationService.Shutdown();
        Logger.Log($"SekaiToolsGUI 退出 (exitCode={e.ApplicationExitCode})");
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _ownsSingleInstanceMutex = false;
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
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
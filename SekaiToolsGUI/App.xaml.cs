using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.General;

namespace SekaiToolsGUI;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\SekaiToolsGUI-1D56E931-7BB9-4E91-B960-76A04EC83C45";
    private bool _ownsSingleInstanceMutex;
    private Mutex? _singleInstanceMutex;
    private int _isShowingErrorDialog;

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
        Logger.Log($"UI线程未处理异常: {e.Exception}", LogLevel.Critical);
        ShowErrorDialog(e.Exception, "UI 线程", true);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Log($"未处理异常(IsTerminating={e.IsTerminating}): {ex}", LogLevel.Critical);
            ShowErrorDialog(ex, "后台线程", e.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Log($"未观察任务异常: {e.Exception}", LogLevel.Error);
        e.SetObserved();
        ShowErrorDialog(e.Exception, "异步任务", false);
    }

    private void ShowErrorDialog(Exception exception, string source, bool isTerminating)
    {
        if (Interlocked.Exchange(ref _isShowingErrorDialog, 1) != 0) return;

        try
        {
            void Show()
            {
                var dialog = new ErrorDialog(exception, source, isTerminating);
                if (Current.MainWindow is { IsLoaded: true } mainWindow && mainWindow != dialog)
                    dialog.Owner = mainWindow;
                dialog.ShowDialog();
                if (isTerminating) Shutdown(-1);
            }

            if (Dispatcher.CheckAccess())
                Show();
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                Dispatcher.Invoke(Show);
        }
        catch (Exception dialogException)
        {
            Logger.Log($"显示错误详情窗口失败: {dialogException}", LogLevel.Error);
            try
            {
                MessageBox.Show($"Sekai Tools 遇到了问题：\n\n{exception.Message}\n\n{exception}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // 应用可能正在终止，无法再安全地显示 UI。
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isShowingErrorDialog, 0);
        }
    }
}
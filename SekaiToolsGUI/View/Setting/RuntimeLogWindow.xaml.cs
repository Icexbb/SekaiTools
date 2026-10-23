using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public partial class RuntimeLogWindow : FluentWindow
{
    private static RuntimeLogWindow? _instance;
    private int _refreshPending;
    private volatile bool _closed;

    public static RuntimeLogWindow Open(Window? owner)
    {
        if (_instance is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return existing;
        }
        var window = new RuntimeLogWindow { Owner = owner };
        _instance = window;
        window.Show();
        return window;
    }

    private RuntimeLogWindow()
    {
        InitializeComponent();
        Service.WindowWorkArea.Attach(this);
        TestErrorButton.Visibility = Debugger.IsAttached ? Visibility.Visible : Visibility.Collapsed;
        InMemoryLogSink.Entries.CollectionChanged += LogsChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            InMemoryLogSink.Entries.CollectionChanged -= LogsChanged;
            LogListBox.ItemsSource = null;
            if (ReferenceEquals(_instance, this)) _instance = null;
        };
        RefreshLogs();
    }

    private void LogsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_closed || Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref _refreshPending, 1) != 0) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            Interlocked.Exchange(ref _refreshPending, 0);
            if (!_closed) RefreshLogs();
        }));
    }

    private void RefreshLogs()
    {
        if (LogListBox == null || LevelFilter == null || _closed) return;
        // Own the UI view: log producers can run on worker threads and hold the sink lock.
        var view = new ListCollectionView(InMemoryLogSink.Snapshot().ToList());
        var minimum = LevelFilter.SelectedIndex switch
        {
            1 => LogLevel.Information, 2 => LogLevel.Warning, 3 => LogLevel.Error, _ => LogLevel.Trace
        };
        view.Filter = item => item is LogEntry entry && entry.Level >= minimum;
        LogListBox.ItemsSource = view;
        if (LogListBox.Items.Count > 0) LogListBox.ScrollIntoView(LogListBox.Items[^1]);
    }

    private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs args) => RefreshLogs();
    private void ClearLogs_Click(object sender, RoutedEventArgs args) => InMemoryLogSink.Clear();
    private void TestError_Click(object sender, RoutedEventArgs args) => throw new InvalidOperationException("这是用于测试错误提示的模拟异常。");
}

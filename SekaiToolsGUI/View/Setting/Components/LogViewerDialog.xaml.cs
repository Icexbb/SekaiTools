using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class LogViewerDialog : ContentDialog
{
    private readonly ICollectionView _logView;

    public LogViewerDialog()
    {
        InitializeComponent();
        BindingOperations.EnableCollectionSynchronization(InMemoryLogSink.Entries, InMemoryLogSink.Entries);
        _logView = CollectionViewSource.GetDefaultView(InMemoryLogSink.Entries);
        _logView.Filter = FilterAll;
        LogListBox.ItemsSource = _logView;

        ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (_, _) =>
        {
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        };
    }

    private static bool FilterAll(object obj)
        => true;

    private static bool FilterInfoPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Information;

    private static bool FilterWarnPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Warning;

    private static bool FilterErrorPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Error;

    private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _logView.Filter = LevelFilter.SelectedIndex switch
        {
            1 => FilterInfoPlus,
            2 => FilterWarnPlus,
            3 => FilterErrorPlus,
            _ => FilterAll
        };
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        InMemoryLogSink.Clear();
    }
}

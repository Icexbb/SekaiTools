using System.Collections.Specialized;
using System.Windows;
using System.Windows.Data;
using SekaiToolsBase;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class LogViewerDialog : ContentDialog
{
    public LogViewerDialog()
    {
        InitializeComponent();
        BindingOperations.EnableCollectionSynchronization(InMemoryLogSink.Entries, InMemoryLogSink.Entries);
        LogListBox.ItemsSource = InMemoryLogSink.Entries;
        ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (_, _) =>
        {
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        };
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        InMemoryLogSink.Clear();
    }
}

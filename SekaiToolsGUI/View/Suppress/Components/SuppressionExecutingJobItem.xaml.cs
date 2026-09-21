using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionExecutingJobItem : UserControl
{
    public SuppressionExecutingJobItem()
    {
        InitializeComponent();
    }

    private void CancelJob_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppressionJobModel job) job.Cancel();
    }

    private void LogTextBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        ScrollLogToEnd();
    }

    private void LogTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ScrollLogToEnd();
    }

    private void ScrollLogToEnd()
    {
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }
}

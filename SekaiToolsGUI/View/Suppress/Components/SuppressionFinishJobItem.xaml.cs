using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionFinishJobItem : UserControl
{
    public event EventHandler? RemoveRequested;

    public SuppressionFinishJobItem()
    {
        InitializeComponent();
    }

    private void ShowFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuppressionJobModel job) return;

        var info = new ProcessStartInfo("Explorer.exe");
        info.ArgumentList.Add("/select," + job.OutputPath);
        Process.Start(info);
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
}

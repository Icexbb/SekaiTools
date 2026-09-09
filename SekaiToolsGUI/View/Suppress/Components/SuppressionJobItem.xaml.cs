using System.Diagnostics;

using System.Windows;
using System.Windows.Controls;


using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionJobItem : UserControl
{
    public SuppressionJobItem()
    {
        InitializeComponent();

    }

    private void CancelJob_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppressionJobModel job) job.Cancel();
    }

    private void ShowFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuppressionJobModel job) return;

        var info = new ProcessStartInfo("Explorer.exe");
        info.ArgumentList.Add("/select," + job.OutputPath);
        Process.Start(info);
    }
}

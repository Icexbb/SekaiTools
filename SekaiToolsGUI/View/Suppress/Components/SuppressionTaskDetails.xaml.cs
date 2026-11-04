using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionTaskDetails : UserControl
{
    public SuppressionTaskDetails()
    {
        InitializeComponent();
    }

    public event EventHandler? EditRequested;
    public event EventHandler? RemoveRequested;
    public event EventHandler? StartRequested;

    private void Start_OnClick(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Edit_OnClick(object sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Remove_OnClick(object sender, RoutedEventArgs e)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppressionJobModel job) job.Cancel();
    }

    private void ShowFile_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuppressionJobModel { IsCompleted: true } job) return;
        var info = new ProcessStartInfo("Explorer.exe");
        info.ArgumentList.Add("/select," + job.OutputPath);
        Process.Start(info);
    }

    private void Log_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (FollowLog?.IsChecked == true) ((TextBox)sender).ScrollToEnd();
    }
}

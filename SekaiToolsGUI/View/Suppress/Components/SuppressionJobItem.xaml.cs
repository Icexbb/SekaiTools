using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiToolsMedia;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionJobItem : UserControl
{
    public SuppressionJobItem()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private SuppressionJobModel? Job { get; set; }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SuppressionJobModel oldJob)
            oldJob.PropertyChanged -= JobOnPropertyChanged;
        Job = e.NewValue as SuppressionJobModel;
        if (Job != null)
            Job.PropertyChanged += JobOnPropertyChanged;
        ApplyStatus();
    }

    private void JobOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SuppressionJobModel.Progress)) return;
        if (Dispatcher.CheckAccess())
            ApplyStatus();
        else
            Dispatcher.BeginInvoke(ApplyStatus);
    }

    private void ApplyStatus()
    {
        var brush = Job?.Progress.State switch
        {
            VideoSuppressionState.Preparing or VideoSuppressionState.Running => new SolidColorBrush(Colors.LightBlue),
            VideoSuppressionState.Cancelling => new SolidColorBrush(Colors.Khaki),
            VideoSuppressionState.Completed => new SolidColorBrush(Colors.LightGreen),
            VideoSuppressionState.Cancelled => new SolidColorBrush(Colors.LightGray),
            VideoSuppressionState.Failed => new SolidColorBrush(Colors.LightPink),
            _ => null
        };
        Control.BorderBrush = brush;
        Control.BorderThickness = new Thickness(2);
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

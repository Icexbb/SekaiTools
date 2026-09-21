using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionQueueJobItem : UserControl
{
    private Point _dragStartPoint;

    public event EventHandler? StartRequested;

    public SuppressionQueueJobItem()
    {
        InitializeComponent();
    }

    private void CancelJob_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppressionJobModel job) job.Cancel();
    }

    private void StartExecuteButton_OnClick(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Control_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
    }

    private void Control_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed ||
            DataContext is not SuppressionJobModel job) return;

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        DragDrop.DoDragDrop(this, job, DragDropEffects.Move);
    }
}

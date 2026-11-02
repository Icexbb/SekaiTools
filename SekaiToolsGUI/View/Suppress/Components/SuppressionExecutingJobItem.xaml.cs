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

    private void ShowLog_OnClick(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var text = new TextBox { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
        text.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("Progress.Log") { Mode = System.Windows.Data.BindingMode.OneWay });
        var dialog = new Window { Title = "压制执行日志", Owner = owner, DataContext = DataContext, Content = text, Width = Math.Min(900, owner?.ActualWidth ?? 900), Height = Math.Min(550, owner?.ActualHeight ?? 550), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
        dialog.ShowDialog();
    }
}

using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View;

public partial class SubtitleMaker : UserControl
{
    public SubtitleMaker()
    {
        InitializeComponent();
    }

    private void CreateTaskControl(object sender, RoutedEventArgs e)
    {
        var taskControl = new TaskControl
        {
            ExpanderSettings =
            {
                IsExpanded = true
            },
            Margin = new Thickness(0, 10, 0, 0)
        };
        TaskPanel.Children.Add(taskControl);
    }
}
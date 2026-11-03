using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionTaskListItem : UserControl
{
    public SuppressionTaskListItem() => InitializeComponent();
    public event EventHandler? Selected;
    public event EventHandler? RemoveRequested;
    private void SelectButton_OnClick(object sender, RoutedEventArgs e) => Selected?.Invoke(this, EventArgs.Empty);
    private void RemoveButton_OnClick(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
}

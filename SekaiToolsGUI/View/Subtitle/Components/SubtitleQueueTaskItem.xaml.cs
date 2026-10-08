using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleQueueTaskItem : UserControl
{
    public SubtitleQueueTaskItem() => InitializeComponent();
    public event EventHandler? Selected;
    public event EventHandler? RemoveRequested;
    private void SelectButton_OnClick(object sender, RoutedEventArgs e) => Selected?.Invoke(this, EventArgs.Empty);
    private void RemoveButton_OnClick(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
}

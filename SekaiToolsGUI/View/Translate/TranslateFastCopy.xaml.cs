using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslateFastCopy : UserControl
{
    public TranslateFastCopy()
    {
        InitializeComponent();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            Clipboard.SetText(button.Content.ToString()!);
        }
    }
}
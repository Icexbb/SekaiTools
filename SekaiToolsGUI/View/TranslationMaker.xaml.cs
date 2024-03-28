using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View;

public partial class TranslationMaker : UserControl
{
    public TranslationMaker()
    {
        InitializeComponent();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var frame = TaskSeparatorSelectorWindow.Get(new("123456789", 0, 600, 60));
        Console.WriteLine(frame[0] + " " + frame[1]);
    }
}
using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View;

public partial class PlaceHolderTextBox : UserControl
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(PlaceHolderTextBox),
        new PropertyMetadata(default(string))
    );

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public PlaceHolderTextBox()
    {
        InitializeComponent();
    }
}
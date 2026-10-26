using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.Control;

/// <summary>Shared export editor; the inherited DataContext owns persistence and option values.</summary>
public partial class ExportOptionsControl : UserControl
{
    public static readonly DependencyProperty ShowResetButtonsProperty = DependencyProperty.Register(
        nameof(ShowResetButtons), typeof(bool), typeof(ExportOptionsControl), new PropertyMetadata(false));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ExportOptionsControl),
        new PropertyMetadata("选择本次输出的类别，仅对本次输出生效"));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(ExportOptionsControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool ShowResetButtons
    {
        get => (bool)GetValue(ShowResetButtonsProperty);
        set => SetValue(ShowResetButtonsProperty, value);
    }
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ExportOptionsControl() => InitializeComponent();
}

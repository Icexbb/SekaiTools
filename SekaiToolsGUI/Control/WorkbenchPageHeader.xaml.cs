using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Control;

public partial class WorkbenchPageHeader : UserControl
{
    public WorkbenchPageHeader() => InitializeComponent();
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(WorkbenchPageHeader), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(WorkbenchPageHeader), new PropertyMetadata(""));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(SymbolRegular), typeof(WorkbenchPageHeader), new PropertyMetadata(SymbolRegular.Document24));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public SymbolRegular Icon { get => (SymbolRegular)GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

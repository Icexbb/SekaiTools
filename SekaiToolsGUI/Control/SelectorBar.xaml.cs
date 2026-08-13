using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.Control;

[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(SelectorBarItem))]
public partial class SelectorBar : ListBox
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(Orientation),
        typeof(SelectorBar),
        new FrameworkPropertyMetadata(Orientation.Horizontal));

    public SelectorBar()
    {
        InitializeComponent();
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is ListBoxItem;
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new SelectorBarItem();
    }
}

public class SelectorBarItem : ListBoxItem;

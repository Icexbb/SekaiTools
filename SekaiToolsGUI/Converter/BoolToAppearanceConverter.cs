using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Converter;

public class BoolToAppearanceConverter : IValueConverter
{
    public bool Invert { get; set; }
    public ControlAppearance TrueToAppearance { get; set; } = ControlAppearance.Primary;
    public ControlAppearance FalseToAppearance { get; set; } = ControlAppearance.Secondary;

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var result = value is true;

        if (Invert)
            result = !result;

        return result
            ? TrueToAppearance
            : FalseToAppearance;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var result = Equals(value, TrueToAppearance);
        return Invert ? !result : result;
    }
}
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Converter;

public class BoolToAppearanceConverter : IValueConverter
{
    public bool Invert { get; set; }

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
            ? ControlAppearance.Primary
            : ControlAppearance.Secondary;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is ControlAppearance.Primary;
    }
}
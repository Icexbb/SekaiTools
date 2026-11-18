using System.Windows;

namespace SekaiToolsGUI.Service;

/// <summary>Opt-in workbench colors; native WPF-UI controls retain their theme and state templates.</summary>
public static class WorkbenchTheme
{
    public static void Apply(int theme)
    {
        var name = theme switch { 0 => "Light", 1 => "Dark", 2 => "HighContrast", _ => null };
        if (name is null || Application.Current is null) return;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("/Resources/Design/Palette.") == true);
        var next = new ResourceDictionary { Source = new Uri($"/SekaiToolsGUI;component/Resources/Design/Palette.{name}.xaml", UriKind.Relative) };
        if (current is null) dictionaries.Add(next);
        else dictionaries[dictionaries.IndexOf(current)] = next;
    }
}

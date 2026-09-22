using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using SekaiToolsCore.Utils;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class FontSelectDialog : ContentDialog
{
    private readonly ICollectionView _fontView;

    public FontSelectDialog(string fontFamily)
    {
        InitializeComponent();

        var fontList = UtilFunc.GetFontFamilyNames()
            .Where(font => !string.IsNullOrWhiteSpace(font))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(font => font, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var fonts = new ObservableCollection<string>(fontList);
        _fontView = CollectionViewSource.GetDefaultView(fonts);
        _fontView.Filter = FilterFont;
        FontListBox.ItemsSource = _fontView;

        FontName = fontList.FirstOrDefault(font =>
            string.Equals(font, fontFamily, StringComparison.OrdinalIgnoreCase))
            ?? fontList.FirstOrDefault()
            ?? "";

        if (FontName != "") FontListBox.SelectedItem = FontName;
        UpdateEmptyState();
    }

    public string FontName { get; private set; } = "";

    private bool FilterFont(object item)
    {
        return item is string font &&
               (string.IsNullOrWhiteSpace(BoxFontFilter.Text) ||
                font.Contains(BoxFontFilter.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }

    private void BoxFontFilter_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _fontView.Refresh();
        UpdateEmptyState();
    }

    private void FontListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontListBox.SelectedItem is string font) FontName = font;
    }

    private void UpdateEmptyState()
    {
        NoFontResultText.Visibility = FontListBox.Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

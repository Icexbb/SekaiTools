using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SkiaSharp;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class FontSelectDialog : ContentDialog
{
    private readonly string _currentFontFamily;
    private ICollectionView? _fontView;
    private bool _isLoading;

    public FontSelectDialog(string fontFamily)
    {
        InitializeComponent();
        _currentFontFamily = fontFamily;
        FontName = fontFamily;
    }

    public string FontName { get; private set; } = "";

    private async void FontSelectDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _fontView != null) return;

        _isLoading = true;
        try
        {
            var fontVariants = await Task.Run(CreateFontVariants);
            var fonts = new ObservableCollection<FontVariant>(fontVariants);

            _fontView = CollectionViewSource.GetDefaultView(fonts);
            _fontView.Filter = FilterFont;
            FontListBox.ItemsSource = _fontView;
            FontListBox.Visibility = Visibility.Visible;
            FontLoadingText.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = fontVariants.Count > 0;

            var selectedFont = fontVariants.FirstOrDefault(font =>
                string.Equals(font.DisplayName, _currentFontFamily, StringComparison.OrdinalIgnoreCase));

            // 兼容旧配置：旧版本只保存字体家族名，打开后默认定位到该家族的第一个变体。
            selectedFont ??= fontVariants.FirstOrDefault(font =>
                string.Equals(font.FamilyName, _currentFontFamily, StringComparison.OrdinalIgnoreCase));

            selectedFont ??= fontVariants.FirstOrDefault();
            if (selectedFont != null)
            {
                FontName = selectedFont.DisplayName;
                FontListBox.SelectedItem = selectedFont;
            }

            UpdateEmptyState();
        }
        catch (Exception)
        {
            FontLoadingText.Text = "字体列表加载失败";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static IReadOnlyList<FontVariant> CreateFontVariants()
    {
        var fontManager = SKFontManager.Default;
        var variants = new List<FontVariant>();

        foreach (var familyName in fontManager.GetFontFamilies()
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var styles = fontManager.GetFontStyles(familyName);
            if (styles.Count == 0)
            {
                variants.Add(new FontVariant(
                    familyName, "Regular", FontWeights.Normal, FontStyles.Normal, FontStretches.Normal));
                continue;
            }

            for (var index = 0; index < styles.Count; index++)
            {
                var style = styles[index];
                var variantName = styles.GetStyleName(index);
                variants.Add(new FontVariant(
                    familyName,
                    string.IsNullOrWhiteSpace(variantName) ? GetVariantDescription(style) : variantName,
                    FontWeight.FromOpenTypeWeight(Math.Clamp(style.Weight, 1, 999)),
                    style.Slant == SKFontStyleSlant.Upright ? FontStyles.Normal : FontStyles.Italic,
                    FontStretch.FromOpenTypeStretch(Math.Clamp(style.Width, 1, 9))));
            }
        }

        return variants
            .OrderBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(font => font.VariantName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string GetVariantDescription(SKFontStyle style)
    {
        var slant = style.Slant == SKFontStyleSlant.Upright ? "" : " Italic";
        return $"{style.Weight}{slant}";
    }

    private bool FilterFont(object item)
    {
        if (item is not FontVariant font) return false;

        var query = BoxFontFilter.Text.Trim();
        return query.Length == 0 ||
               font.FamilyName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               font.VariantName.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void BoxFontFilter_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _fontView?.Refresh();
        UpdateEmptyState();
    }

    private void FontListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontListBox.SelectedItem is FontVariant font) FontName = font.DisplayName;
    }

    private void UpdateEmptyState()
    {
        if (_fontView == null || FontListBox.Visibility != Visibility.Visible)
        {
            NoFontResultText.Visibility = Visibility.Collapsed;
            return;
        }

        NoFontResultText.Visibility = FontListBox.Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private sealed record FontVariant(
        string FamilyName,
        string VariantName,
        FontWeight FontWeight,
        FontStyle FontStyle,
        FontStretch FontStretch)
    {
        public string DisplayName => $"{FamilyName} {VariantName}";
    }
}

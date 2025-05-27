using System.Windows.Controls;
using SekaiToolsCore.Utils;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class FontSelectDialog : ContentDialog
{
    public FontSelectDialog(string fontFamily)
    {
        InitializeComponent();
        var fontList = UtilFunc.GetFontFamilyNames().ToArray();
        foreach (var font in fontList) BoxFontName.Items.Add(font);
        if (fontList.Contains(fontFamily)) BoxFontName.SelectedItem = fontFamily;
        else BoxFontName.SelectedIndex = 0;
    }

    public string FontName { get; private set; } = "";

    private void BoxFontName_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FontName = (string)BoxFontName.SelectedItem;
    }
}
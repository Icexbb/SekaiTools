using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public class FontSelectDialogModel : ViewModelBase
{
    public string FontFamily
    {
        get => GetProperty("Arial");
        set => SetProperty(value);
    }

    // public int FontSize
    // {
    //     get => GetProperty(12);
    //     set => SetProperty(value);
    // }
    //
    // public bool IsBold
    // {
    //     get => GetProperty(false);
    //     set => SetProperty(value);
    // }
    //
    // public bool IsItalic
    // {
    //     get => GetProperty(false);
    //     set => SetProperty(value);
    // }
    //
    // public bool IsUnderline
    // {
    //     get => GetProperty(false);
    //     set => SetProperty(value);
    // }
    //
    // public bool IsStrikeout
    // {
    //     get => GetProperty(false);
    //     set => SetProperty(value);
    // }

    public FontSelectDialogModel(string fontFamily)
    {
        FontFamily = fontFamily;
    }
}

public partial class FontSelectDialog : ContentDialog
{
    public string FontName { get; private set; } = "";

    public FontSelectDialog(string fontFamily)
    {
        InitializeComponent();
        var fontList = new InstalledFontCollection().Families.Select(family => family.Name).ToList();
        foreach (var font in fontList) BoxFontName.Items.Add(font);
        if (fontList.Contains(fontFamily)) BoxFontName.SelectedItem = fontFamily;
        else BoxFontName.SelectedIndex = 0;
    }

    private void BoxFontName_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FontName = (string)BoxFontName.SelectedItem;
    }
}
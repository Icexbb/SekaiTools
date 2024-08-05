using System.Drawing.Text;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class FontSelectDialog : ContentDialog
{
    public FontSelectDialog(string fontFamily)
    {
        InitializeComponent();
        var collection = new InstalledFontCollection();
        var fontList = collection.Families.Select(family => family.Name).ToList();
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
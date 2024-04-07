using System.Windows.Controls;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslateLineEmpty : UserControl, IExportable
{
    public TranslateLineEmpty()
    {
        InitializeComponent();
    }

    public string Export()
    {
        return "";
    }
}
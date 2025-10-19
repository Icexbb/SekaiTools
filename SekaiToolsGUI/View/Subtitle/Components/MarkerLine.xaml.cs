using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class MarkerLine : UserControl, INavigableView<MarkerLineModel>
{
    public MarkerLine(MarkerFrameSet set)
    {
        DataContext = new MarkerLineModel(set);
        InitializeComponent();
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }

    public MarkerLineModel ViewModel => (MarkerLineModel)DataContext;

    private void TextBlockContent_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextBlockContent.Text = ViewModel.Set.Data.BodyOriginal;
    }

    private void TextBlockContent_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }
}
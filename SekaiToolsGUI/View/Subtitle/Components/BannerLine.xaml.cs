using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class BannerLine : UserControl, INavigableView<BannerLineModel>
{
    public BannerLine(BannerFrameSet set)
    {
        DataContext = new BannerLineModel(set);
        InitializeComponent();
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }

    public BannerLineModel ViewModel => (BannerLineModel)DataContext;

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
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle;

public class BannerLineModel(BannerFrameSet set) : ViewModelBase
{
    public readonly BannerFrameSet Set = set;

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();
}

public partial class BannerLine : UserControl, INavigableView<BannerLineModel>
{
    public BannerLineModel ViewModel => (BannerLineModel)DataContext;

    public BannerLine(BannerFrameSet set)
    {
        DataContext = new BannerLineModel(set);
        InitializeComponent();
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }

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
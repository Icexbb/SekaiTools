using Avalonia.Controls;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsAvalonia.ViewModel.Subtitle;

namespace SekaiToolsAvalonia.View.Subtitle.Components;

public partial class BannerLine : UserControl
{
    public BannerLine(BannerBaseFrameSet set)
    {
        DataContext = new BannerLineModel(set);
        InitializeComponent();
    }

    public BannerLineModel ViewModel => (BannerLineModel)DataContext!;
}

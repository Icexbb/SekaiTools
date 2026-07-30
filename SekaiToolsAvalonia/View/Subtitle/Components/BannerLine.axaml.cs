using Avalonia.Controls;
using SekaiToolsAvalonia.ViewModel.Subtitle;
using SekaiToolsCore.Process.FrameSet;

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
using System.Windows.Controls;
using SekaiToolsCore.Process;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle;

public class BannerLineModel(BannerFrameSet set) : ViewModelBase
{
    private readonly FrameRate _frameRate = set.Fps;
    public readonly BannerFrameSet Set = set;

    public int Index => Set.Data.Index;
    public string Content => Set.Data.BodyOriginal;

    public int StartFrame => Set.Start().Index;
    public string StartTime => Set.StartTime();
    public int EndFrame => Set.End().Index;
    public string EndTime => Set.EndTime();
}

public partial class BannerLine : UserControl, INavigableView<BannerLineModel>
{
    public BannerLineModel ViewModel => (BannerLineModel)DataContext;

    public BannerLine(BannerFrameSet set)
    {
        DataContext = new BannerLineModel(set);
        InitializeComponent();
    }
}
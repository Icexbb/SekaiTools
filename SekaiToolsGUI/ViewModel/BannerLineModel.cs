using SekaiToolsCore.Process;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsGUI.ViewModel;

public class BannerLineModel(BannerFrameSet set) : ViewModelBase
{
    public readonly BannerFrameSet Set = set;

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();
}
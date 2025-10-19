using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class BannerLineModel(BannerFrameSet set) : ViewModelBase
{
    public readonly BannerFrameSet Set = set;

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();
}
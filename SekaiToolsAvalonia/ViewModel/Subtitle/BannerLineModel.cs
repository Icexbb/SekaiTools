using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsAvalonia.ViewModel.Subtitle;

public class BannerLineModel(BannerBaseFrameSet set) : ViewModelBase
{
    public readonly BannerBaseFrameSet Set = set;
    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();
}

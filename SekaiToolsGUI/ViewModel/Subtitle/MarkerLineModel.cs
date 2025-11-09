using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class MarkerLineModel(MarkerBaseFrameSet set) : ViewModelBase
{
    private readonly FrameRate _frameRate = set.Fps;
    public readonly MarkerBaseFrameSet Set = set;

    public int Index => Set.Data.Index;
    public string Content => Set.Data.BodyOriginal;

    public int StartFrame => Set.Start().Index;
    public string StartTime => Set.StartTime();
    public int EndFrame => Set.End().Index;
    public string EndTime => Set.EndTime();
}
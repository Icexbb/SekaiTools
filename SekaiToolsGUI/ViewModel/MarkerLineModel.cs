using SekaiToolsCore.Process;

namespace SekaiToolsGUI.ViewModel;

public class MarkerLineModel(MarkerFrameSet set) : ViewModelBase
{
    private readonly FrameRate _frameRate = set.Fps;
    public readonly MarkerFrameSet Set = set;

    public int Index => Set.Data.Index;
    public string Content => Set.Data.BodyOriginal;

    public int StartFrame => Set.Start().Index;
    public string StartTime => Set.StartTime();
    public int EndFrame => Set.End().Index;
    public string EndTime => Set.EndTime();
}
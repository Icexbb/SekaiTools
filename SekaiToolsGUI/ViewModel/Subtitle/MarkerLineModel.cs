using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class MarkerLineModel(MarkerBaseFrameSet set) : ViewModelBase
{
    public readonly MarkerBaseFrameSet Set = set;

    public int Index => Set.Data.Index;
    public string Content => RawContent;
    public string RawContent => Set.Data.BodyOriginal;

    public string TranslatedContent
    {
        get => GetProperty(Set.Data.BodyTranslated);
        set
        {
            SetProperty(value);
            Set.Data.BodyTranslated = value;
        }
    }

    public int StartFrame => Set.Start().Index;
    public string StartTime => Set.StartTime();
    public int EndFrame => Set.End().Index;
    public string EndTime => Set.EndTime();
    public string EventDuration => GetEventDuration();

    private string GetEventDuration()
    {
        var start = Set.Start();
        var end = Set.End();
        var startMilliseconds = start.Fps.TimeAtFrame(start.Index, FrameType.Start).Milliseconds;
        var endMilliseconds = end.Fps.TimeAtFrame(end.Index, FrameType.End).Milliseconds;
        return $"{Math.Max(0, endMilliseconds - startMilliseconds) / 1000d:0.0}s";
    }
}

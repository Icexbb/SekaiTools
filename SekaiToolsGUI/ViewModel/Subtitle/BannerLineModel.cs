using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class BannerLineModel(BannerBaseFrameSet set) : ViewModelBase
{
    public readonly BannerBaseFrameSet Set = set;

    public int EventIndex => Set.Data.EffectiveStoryIndex;
    public string EventNumber => $"#{EventIndex + 1}";
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

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();
    public string EventDuration => GetEventDuration();

    public void RefreshTiming()
    {
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(EventDuration));
    }

    private string GetEventDuration()
    {
        var start = Set.Start();
        var end = Set.End();
        var startMilliseconds = start.Fps.TimeAtFrame(start.Index, FrameType.Start).Milliseconds;
        var endMilliseconds = end.Fps.TimeAtFrame(end.Index, FrameType.End).Milliseconds;
        return $"{Math.Max(0, endMilliseconds - startMilliseconds) / 1000d:0.0}s";
    }
}

using SekaiToolsCore.Story.Event;

namespace SekaiToolsGUI.ViewModel;

public class LineEffectModel : ViewModelBase
{
    private readonly Event _event;

    public LineEffectModel(Event eEvent)
    {
        _event = eEvent;
        OriginalContent = _event.BodyOriginal;
        TranslatedContent = _event.BodyTranslated;
    }


    public string OriginalContent
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public Event Export()
    {
        switch (_event)
        {
            case Banner:
                return ExportBanner();
            case Marker:
                return ExportMarker();
            default:
                var e = (Event)_event.Clone();
                e.BodyTranslated = TranslatedContent;
                return e;
        }
    }

    private Banner ExportBanner()
    {
        var banner = (Banner)_event.Clone();
        banner.BodyTranslated = TranslatedContent;
        return banner;
    }

    private Marker ExportMarker()
    {
        var marker = (Marker)_event.Clone();
        marker.BodyTranslated = TranslatedContent;
        return marker;
    }
}
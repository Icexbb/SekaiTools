using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsGUI.ViewModel.Translate;

public class LineEffectModel : ViewModelBase
{
    private readonly BaseStoryEvent _baseStoryEvent;

    public LineEffectModel(BaseStoryEvent eBaseStoryEvent)
    {
        _baseStoryEvent = eBaseStoryEvent;
        OriginalContent = _baseStoryEvent.BodyOriginal;
        TranslatedContent = _baseStoryEvent.BodyTranslated;
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

    public BaseStoryEvent Export()
    {
        switch (_baseStoryEvent)
        {
            case BannerStoryEvent:
                return ExportBanner();
            case MarkerStoryEvent:
                return ExportMarker();
            default:
                var e = (BaseStoryEvent)_baseStoryEvent.Clone();
                e.BodyTranslated = TranslatedContent;
                return e;
        }
    }

    private BannerStoryEvent ExportBanner()
    {
        var banner = (BannerStoryEvent)_baseStoryEvent.Clone();
        banner.BodyTranslated = TranslatedContent;
        return banner;
    }

    private MarkerStoryEvent ExportMarker()
    {
        var marker = (MarkerStoryEvent)_baseStoryEvent.Clone();
        marker.BodyTranslated = TranslatedContent;
        return marker;
    }
}
using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsMauiText.ViewModel;

public class LineEffectModel : LineModel
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
        set
        {
            SetProperty(value);
            if (ContentTranslateChangedEnabled)
                ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ContentReference
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasContentReference));
        }
    }

    public bool HasContentReference => !string.IsNullOrEmpty(ContentReference);
    public bool ContentTranslateChangedEnabled { get; set; } = true;
    public event EventHandler? ContentTranslateChanged;

    public override string Result =>
        string.IsNullOrWhiteSpace(TranslatedContent) ? OriginalContent : TranslatedContent;

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
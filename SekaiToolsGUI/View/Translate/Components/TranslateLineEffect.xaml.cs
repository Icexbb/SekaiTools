using System.Windows.Controls;
using Wpf.Ui.Controls;
using SekaiBanner = SekaiToolsCore.Story.Event.Banner;
using SekaiMarker = SekaiToolsCore.Story.Event.Marker;
using SekaiEvent = SekaiToolsCore.Story.Event.Event;

namespace SekaiToolsGUI.View.Translate.Components;

public class LineEffectModel : ViewModelBase
{
    private readonly SekaiEvent _event;

    public LineEffectModel(SekaiEvent eEvent)
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

    public SekaiEvent Export()
    {
        switch (_event)
        {
            case SekaiBanner:
                return ExportBanner();
            case SekaiMarker:
                return ExportMarker();
            default:
                var e = (SekaiEvent)_event.Clone();
                e.BodyTranslated = TranslatedContent;
                return e;
        }
    }

    private SekaiBanner ExportBanner()
    {
        var banner = (SekaiBanner)_event.Clone();
        banner.BodyTranslated = TranslatedContent;
        return banner;
    }

    private SekaiMarker ExportMarker()
    {
        var marker = (SekaiMarker)_event.Clone();
        marker.BodyTranslated = TranslatedContent;
        return marker;
    }
}

public partial class TranslateLineEffect : UserControl, INavigableView<LineEffectModel>, IExportable
{
    public TranslateLineEffect(SekaiEvent eEvent)
    {
        DataContext = new LineEffectModel(eEvent);
        InitializeComponent();
    }

    public string Export()
    {
        var result = ViewModel.TranslatedContent;
        return string.IsNullOrWhiteSpace(result) ? "地点" : result;
    }

    public LineEffectModel ViewModel => (LineEffectModel)DataContext;
}
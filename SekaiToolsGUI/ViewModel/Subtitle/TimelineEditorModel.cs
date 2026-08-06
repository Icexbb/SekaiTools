namespace SekaiToolsGUI.ViewModel.Subtitle;

public class TimelineEditorModel : ViewModelBase
{
    public bool ShowTimeLine
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }
}
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class QuickEditDialogModel : ViewModelBase
{
    public QuickEditDialogModel(BaseStoryEvent storyEvent, bool canReturn = false, bool useReturn = false)
    {
        ContentOriginal = storyEvent.BodyOriginal;
        ContentTranslated = storyEvent.BodyTranslated;
        if (ContentTranslated.Contains("\\R"))
            ContentTranslated = ContentTranslated.Replace("\n", "")
                .Replace("\\N", "").Replace("\\R", "\n");
        else
            ContentTranslated = ContentTranslated.Replace("\\N", "\n");

        if (ContentTranslated.LineCount() == 3)
            ContentTranslated = ContentTranslated.Replace("\n", "");

        CanReturn = canReturn;
        UseReturn = CanReturn && useReturn;
    }

    public string ContentOriginal
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string ContentTranslated
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public bool CanReturn { get; }

    public bool UseReturn
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }
}

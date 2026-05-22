using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsAvalonia.ViewModel.Subtitle;

public class QuickEditDialogModel : ViewModelBase
{
    public QuickEditDialogModel(DialogBaseFrameSet dialogBase)
    {
        ContentOriginal = dialogBase.Data.BodyOriginal;
        ContentTranslated = dialogBase.Data.BodyTranslated;
        if (ContentTranslated.Contains("\\R"))
            ContentTranslated = ContentTranslated.Replace("\n", "").Replace("\\N", "").Replace("\\R", "\n");
        else
            ContentTranslated = ContentTranslated.Replace("\\N", "\n");
        if (ContentTranslated.LineCount() == 3)
            ContentTranslated = ContentTranslated.Replace("\n", "");
        CanReturn = dialogBase.Data.BodyOriginal.LineCount() == 3;
        UseReturn = CanReturn && dialogBase.UseSeparator;
    }

    public string ContentOriginal { get => GetProperty(""); set => SetProperty(value); }
    public string ContentTranslated { get => GetProperty(""); set => SetProperty(value); }
    public bool CanReturn { get; }
    public bool UseReturn { get => GetProperty(false); set => SetProperty(value); }

    public static string NormalContent(string str) => str.Replace("\\R", "\n").Replace("\\N", "\n").Trim();
    public static string LineContent(string str) => NormalContent(str).Replace("\n", "").Trim();
}

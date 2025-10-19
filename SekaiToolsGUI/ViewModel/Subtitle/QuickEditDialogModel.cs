using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Utils;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class QuickEditDialogModel : ViewModelBase
{
    public QuickEditDialogModel(DialogFrameSet dialog)
    {
        // Dialog = dialog;
        ContentOriginal = dialog.Data.BodyOriginal;
        ContentTranslated = dialog.Data.BodyTranslated;
        if (ContentTranslated.Contains("\\R"))
            ContentTranslated = ContentTranslated.Replace("\n", "")
                .Replace("\\N", "").Replace("\\R", "\n");
        else
            ContentTranslated = ContentTranslated.Replace("\\N", "\n");

        if (ContentTranslated.LineCount() == 3)
            ContentTranslated = ContentTranslated.Replace("\n", "");

        CanReturn = dialog.Data.BodyOriginal.LineCount() == 3;
        UseReturn = CanReturn && dialog.UseSeparator;
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

    // private Dialog Dialog { get; }
    private static string NormalContent(string str)
    {
        return str.Replace("\\R", "\n")
            .Replace("\\N", "\n")
            .Trim();
    }

    private static string LineContent(string str)
    {
        return NormalContent(str).Replace("\n", "").Trim();
    }
}
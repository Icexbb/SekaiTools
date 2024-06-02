using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;
using SekaiDialog = SekaiToolsCore.Story.Event.Dialog;

namespace SekaiToolsGUI.View.Translate;

public class LineDialogModel : ViewModelBase
{
    private SekaiDialog Dialog { get; }

    public LineDialogModel(SekaiDialog dialog)
    {
        Dialog = dialog;
        OriginalCharacter = dialog.CharacterOriginal;
        OriginalContent = dialog.BodyOriginal;
        if (dialog.CharacterTranslated != string.Empty)
        {
            TranslatedCharacter = dialog.CharacterTranslated;
        }

        TranslatedContent = dialog.BodyTranslated;
    }

    public SekaiDialog Export()
    {
        var dialog = new SekaiDialog(
            Dialog.Index,
            OriginalContent,
            Dialog.CharacterId,
            OriginalCharacter,
            Dialog.CloseWindow,
            Dialog.Shake
        );
        dialog.SetTranslation(TranslatedCharacter, TranslatedContent);
        return dialog;
    }

    public string Icon => Dialog.CharacterId is > 0 and <= 31
        ? $"pack://application:,,,/Resource/Characters/chr_{Dialog.CharacterId}.png"
        // ? ""
        : string.Empty;

    public string OriginalCharacter
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }


    public string TranslatedCharacter
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
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
}

public partial class TranslateLineDialog : UserControl, INavigableView<LineDialogModel>, IExportable
{
    public LineDialogModel ViewModel => (LineDialogModel)DataContext;

    public TranslateLineDialog(SekaiDialog dialog)
    {
        DataContext = new LineDialogModel(dialog);
        InitializeComponent();
    }

    private void TranslatedContentTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((TranslatedContentTextBox.Text + "\n").LineCount() == 3)
                e.Handled = true;
        }
    }

    public string Export()
    {
        return (ViewModel.TranslatedCharacter == ""
                   ? ViewModel.OriginalCharacter
                   : ViewModel.TranslatedCharacter)
               + "："
               + (ViewModel.TranslatedContent == ""
                   ? ViewModel.OriginalContent
                   : ViewModel.TranslatedContent).Replace("\n", "\\N");
    }
}
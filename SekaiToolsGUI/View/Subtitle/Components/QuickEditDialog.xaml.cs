using System.Windows;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

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

public partial class QuickEditDialog : ContentDialog
{
    public QuickEditDialog(DialogFrameSet dialog)
    {
        DataContext = new QuickEditDialogModel(dialog);
        InitializeComponent();
        SwitchCanReturn.Visibility = ViewModel.CanReturn ? Visibility.Visible : Visibility.Collapsed;
    }

    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext;

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key != Key.Enter) return;
        var lineCount = textBox.LineCount;
        if (lineCount >= 2) e.Handled = true; // 阻止回车键输入新行
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
        var newLineCount = newText.Split('\n').Length;

        if (newLineCount > 2) e.Handled = true; // 阻止输入导致超过三行
    }
}
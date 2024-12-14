using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Abstractions.Controls;
using SekaiDialog = SekaiToolsCore.Story.Event.Dialog;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineDialog : UserControl, INavigableView<LineDialogModel>, IExportable
{
    public TranslateLineDialog(SekaiDialog dialog)
    {
        DataContext = new LineDialogModel(dialog);
        InitializeComponent();
    }

    public string Export()
    {
        var character = ViewModel.TranslatedCharacter != ""
            ? ViewModel.TranslatedCharacter
            : ViewModel.OriginalCharacter;
        var content = ViewModel.TranslatedContent;
        return $"{character}：{content.Replace("\n", "\\N")}";
    }

    public LineDialogModel ViewModel => (LineDialogModel)DataContext;


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
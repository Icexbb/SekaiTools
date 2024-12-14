using System.Windows;
using System.Windows.Input;
using SekaiToolsCore.Process;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

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
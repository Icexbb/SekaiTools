using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsCore.Story.Event;
using Wpf.Ui;

namespace SekaiToolsGUI.View.Subtitle;

public class QuickEditDialogModel : ViewModelBase
{
    public QuickEditDialogModel(Dialog dialog)
    {
        // Dialog = dialog;
        ContentOriginal = dialog.BodyOriginal;
        ContentTranslated = dialog.BodyTranslated;
        if (ContentTranslated.Contains("\\R"))
        {
            ContentTranslated = ContentTranslated.Replace("\n", "")
                .Replace("\\N", "").Replace("\\R", "\n");
        }
        else
        {
            ContentTranslated = ContentTranslated.Replace("\\N", "\n");
        }

        if (ContentTranslated.LineCount() == 3)
            ContentTranslated = ContentTranslated.Replace("\n", "");

        CanReturn = dialog.BodyOriginal.LineCount() == 3;
        UseReturn = CanReturn && LineContent(dialog.BodyTranslated).Length > 37;
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

    public bool CanReturn
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool UseReturn
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }
}

public partial class QuickEditDialog : Wpf.Ui.Controls.ContentDialog
{
    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext;

    public QuickEditDialog(Dialog dialog)
    {
        DataContext = new QuickEditDialogModel(dialog);
        InitializeComponent();
        SwitchCanReturn.Visibility = ViewModel.CanReturn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (e.Key == Key.Enter)
            {
                var lineCount = textBox.LineCount;
                if (lineCount >= 2)
                {
                    e.Handled = true; // 阻止回车键输入新行
                }
            }
        }
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            var newLineCount = newText.Split('\n').Length;

            if (newLineCount > 2)
            {
                e.Handled = true; // 阻止输入导致超过三行
            }
        }
    }
}
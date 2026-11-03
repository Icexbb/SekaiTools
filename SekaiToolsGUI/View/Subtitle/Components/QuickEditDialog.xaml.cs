using System.Windows;
using System.Windows.Input;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class QuickEditDialog : ContentDialog
{
    public QuickEditDialog(DialogBaseFrameSet dialogBase) : this(dialogBase.Data)
    {
    }

    public QuickEditDialog(BannerBaseFrameSet bannerBase) : this(bannerBase.Data)
    {
    }

    public QuickEditDialog(MarkerBaseFrameSet markerBase) : this(markerBase.Data)
    {
    }

    private QuickEditDialog(BaseStoryEvent storyEvent)
    {
        DataContext = new QuickEditDialogModel(storyEvent);
        InitializeComponent();
        DataObject.AddPastingHandler(TextBoxTranslated, OnPasting);
    }

    private static bool HasTooManyLines(string text) => text.ReplaceLineEndings("\n").Split('\n').Length > 2;

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) return;
        var pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "";
        var box = TextBoxTranslated;
        var text = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, pasted);
        if (HasTooManyLines(text)) e.CancelCommand();
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary && HasTooManyLines(TextBoxTranslated.Text))
        {
            TextBoxTranslated.Focus();
            return;
        }
        base.OnButtonClick(button);
    }

    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext;

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key != Key.Enter) return;
        var text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, "\n");
        if (HasTooManyLines(text)) e.Handled = true;
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, e.Text);
        if (HasTooManyLines(newText)) e.Handled = true;
    }
}
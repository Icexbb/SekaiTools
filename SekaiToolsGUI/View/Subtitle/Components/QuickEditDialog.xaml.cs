using System.Windows;
using System.Windows.Input;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class QuickEditDialog : ContentDialog
{
    public QuickEditDialog(DialogBaseFrameSet dialogBase)
        : this(dialogBase.Data, dialogBase.Data.BodyOriginal.LineCount() == 3, dialogBase.UseSeparator)
    {
    }

    public QuickEditDialog(BannerBaseFrameSet bannerBase) : this(bannerBase.Data)
    {
    }

    public QuickEditDialog(MarkerBaseFrameSet markerBase) : this(markerBase.Data)
    {
    }

    private QuickEditDialog(BaseStoryEvent storyEvent, bool canReturn = false, bool useReturn = false)
    {
        DataContext = new QuickEditDialogModel(storyEvent, canReturn, useReturn);
        InitializeComponent();
        SwitchCanReturn.Visibility = ViewModel.CanReturn ? Visibility.Visible : Visibility.Collapsed;
    }

    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext;

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key != Key.Enter) return;
        var lineCount = textBox.LineCount;
        if (lineCount >= 2) e.Handled = true;
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
        var newLineCount = newText.Split('\n').Length;

        if (newLineCount > 2) e.Handled = true;
    }
}

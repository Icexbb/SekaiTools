using Avalonia.Controls;
using Avalonia.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsAvalonia.ViewModel.Subtitle;

namespace SekaiToolsAvalonia.View.Subtitle.Components;

public partial class DialogLine : UserControl
{
    public DialogLine(DialogBaseFrameSet set)
    {
        set.InitSeparator();
        DataContext = new DialogLineModel(set);
        InitializeComponent();
        TextContentPreview.Text = ViewModel.TranslatedContent;
        TextBlockCharacter.Text = ViewModel.Set.Data.CharacterTranslated.Length > 0
            ? ViewModel.Set.Data.CharacterTranslated
            : ViewModel.Set.Data.CharacterOriginal;
    }

    public DialogLineModel ViewModel => (DialogLineModel)DataContext!;

    private void TextContentPreview_OnPointerEnter(object? sender, PointerEventArgs e)
    {
        TextContentPreview.Text = ViewModel.RawContent;
    }

    private void TextContentPreview_OnPointerLeave(object? sender, PointerEventArgs e)
    {
        TextContentPreview.Text = ViewModel.TranslatedContent;
    }

    private async void DialogLine_OnDoubleClick(object? sender, TappedEventArgs e)
    {
        var dialog = new QuickEditDialog(ViewModel.Set);
        var result = await dialog.ShowAndWaitAsync(
            TopLevel.GetTopLevel(this) as Window);

        if (result.Edited == null) return;

        var set = ViewModel.Set;
        ViewModel.TranslatedContent = result.Edited;
        DataContext = new DialogLineModel(set);
        ViewModel.UseSeparator = result.UseReturn;
        if (result.Edited.Contains('\n'))
        {
            var parts = result.Edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }
    }
}

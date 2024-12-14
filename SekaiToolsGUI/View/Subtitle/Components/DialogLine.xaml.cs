using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class DialogLine : UserControl, INavigableView<DialogLineModel>
{
    public DialogLine(DialogFrameSet set)
    {
        set.InitSeparator();
        DataContext = new DialogLineModel(set);
        InitializeComponent();
        CheckLineExpander();
        TextContentPreview.Text = ViewModel.TranslatedContent;
        TextBlockCharacter.Text = ViewModel.Set.Data.CharacterTranslated.Length > 0
            ? ViewModel.Set.Data.CharacterTranslated
            : ViewModel.Set.Data.CharacterOriginal;
    }

    public DialogLineModel ViewModel => (DialogLineModel)DataContext;

    private void CheckLineExpander()
    {
        Dispatcher.Invoke(() =>
        {
            PanelSeparator.Visibility = ViewModel.UseSeparator ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async void DialogLine_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new QuickEditDialog(ViewModel.Set);

        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var set = ViewModel.Set;
        var edited = dialog.ViewModel.ContentTranslated;
        ViewModel.TranslatedContent = dialog.ViewModel.ContentTranslated;

        DataContext = new DialogLineModel(set);
        ViewModel.UseSeparator = dialog.ViewModel.UseReturn;
        if (edited.Contains('\n'))
        {
            var parts = edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }

        CheckLineExpander();
    }

    private void TextContentPreview_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextContentPreview.Text = ViewModel.RawContent;
    }

    private void TextContentPreview_OnMouseLeave(object sender, MouseEventArgs e)
    {
        TextContentPreview.Text = ViewModel.TranslatedContent;
    }

    private void TextBlockCharacter_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextBlockCharacter.Text = ViewModel.Set.Data.CharacterOriginal;
    }

    private void TextBlockCharacter_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel.Set.Data.CharacterTranslated.Length > 0)
            TextBlockCharacter.Text = ViewModel.Set.Data.CharacterTranslated;
    }
}
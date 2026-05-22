using Avalonia.Controls;
using SekaiToolsAvalonia.ViewModel.Translate;

namespace SekaiToolsAvalonia.View.Translate.Components;

public partial class TranslateLineDialog : UserControl
{
    public TranslateLineDialog() => InitializeComponent();
    public LineDialogModel? ViewModel => DataContext as LineDialogModel;
}

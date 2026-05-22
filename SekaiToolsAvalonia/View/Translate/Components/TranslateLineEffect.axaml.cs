using Avalonia.Controls;
using SekaiToolsAvalonia.ViewModel.Translate;

namespace SekaiToolsAvalonia.View.Translate.Components;

public partial class TranslateLineEffect : UserControl
{
    public TranslateLineEffect() => InitializeComponent();
    public LineEffectModel? ViewModel => DataContext as LineEffectModel;
}

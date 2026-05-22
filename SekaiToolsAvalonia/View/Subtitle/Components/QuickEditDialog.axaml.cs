using Avalonia.Controls;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsAvalonia.ViewModel.Subtitle;

namespace SekaiToolsAvalonia.View.Subtitle.Components;

public partial class QuickEditDialog : Window
{
    private readonly TaskCompletionSource<(string?, bool)> _tcs = new();
    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext!;

    public QuickEditDialog(DialogBaseFrameSet dialogBase)
    {
        DataContext = new QuickEditDialogModel(dialogBase);
        InitializeComponent();
        SwitchCanReturn.IsVisible = ViewModel.CanReturn;
    }

    public async Task<(string? Edited, bool UseReturn)> ShowAndWaitAsync(Window owner)
    {
        await base.ShowDialog(owner);
        return await _tcs.Task;
    }

    private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tcs.TrySetResult((ViewModel.ContentTranslated, ViewModel.UseReturn));
        Close();
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tcs.TrySetResult((null, false));
        Close();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using SekaiToolsAvalonia.ViewModel.Subtitle;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsAvalonia.View.Subtitle.Components;

public partial class QuickEditDialog : Window
{
    private readonly TaskCompletionSource<(string?, bool)> _tcs = new();

    public QuickEditDialog(DialogBaseFrameSet dialogBase)
    {
        DataContext = new QuickEditDialogModel(dialogBase);
        InitializeComponent();
        SwitchCanReturn.IsVisible = ViewModel.CanReturn;
    }

    public QuickEditDialogModel ViewModel => (QuickEditDialogModel)DataContext!;

    public async Task<(string? Edited, bool UseReturn)> ShowAndWaitAsync(Window owner)
    {
        await ShowDialog(owner);
        return await _tcs.Task;
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult((ViewModel.ContentTranslated, ViewModel.UseReturn));
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult((null, false));
        Close();
    }
}
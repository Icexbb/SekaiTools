using System.Windows;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Service;

internal static class ExportOverwriteConfirmation
{
    public static async Task<bool> ConfirmAsync(IContentDialogService service, SaveFileDialogModel model,
        CancellationToken token)
    {
        if (!model.NeedsOverwriteConfirmation) return true;
        var dialog = new ContentDialog
        {
            Title = "覆盖已有文件？",
            Content = new System.Windows.Controls.TextBlock
            {
                Text = $"以下文件已存在，是否覆盖？\n\n{model.FileName}",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 500
            },
            PrimaryButtonText = "覆盖",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        return await service.ShowAsync(dialog, token) == ContentDialogResult.Primary;
    }
}

using System.Windows;
using System.IO;
using SekaiToolsGUI.ViewModel.Suppress;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionTaskDialog : ContentDialog
{
    public SuppressionTaskDialog(ContentDialogHost contentPresenter) : base(contentPresenter)
    {
        ViewModel = new SuppressTaskSettingsModel();
        DataContext = ViewModel;
        InitializeComponent();
        TaskSettings.DataContext = ViewModel;
        QueueHint.Text = "添加后自动按顺序压制；取消当前任务后继续下一项。";
    }

    public SuppressTaskSettingsModel ViewModel { get; }
    public Func<string>? ValidateSubmission { get; set; }
    public bool OverwriteAuthorized { get; private set; }
    public Func<string>? CommitSubmission { get; set; }
    internal Func<string, Task<bool>>? ConfirmOverwrite { get; set; }
    private bool _submitting;

    private async Task<bool> ShowOverwriteConfirmationAsync(string outputPath)
    {
        var confirmation = new Wpf.Ui.Controls.MessageBox
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = "覆盖已有输出文件？",
            Content = $"输出文件已存在：\n{outputPath}\n\n仅在压制成功后替换已有文件。是否继续？",
            PrimaryButtonText = "继续覆盖",
            CloseButtonText = "返回修改"
        };
        return await confirmation.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    internal async Task<bool> TrySubmitAsync()
    {
        if (_submitting || !ViewModel.CanSubmit) return false;
        _submitting = true;
        IsEnabled = false;
        OverwriteAuthorized = false;
        try
        {
            ViewModel.SubmissionError = ValidateSubmission?.Invoke() ?? "";
            if (ViewModel.SubmissionError.Length > 0) return false;
            var outputPath = Path.GetFullPath(ViewModel.OutputPath);
            if (ViewModel.OutputExists)
            {
                if (!await (ConfirmOverwrite?.Invoke(outputPath) ?? ShowOverwriteConfirmationAsync(outputPath))) return false;
                if (!string.Equals(outputPath, Path.GetFullPath(ViewModel.OutputPath), StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.SubmissionError = "输出路径已改变，请重新提交确认。";
                    return false;
                }
                OverwriteAuthorized = true;
            }
            if (!ViewModel.CanSubmit) { ViewModel.SubmissionError = ViewModel.ConfigError; return false; }
            ViewModel.SubmissionError = ValidateSubmission?.Invoke() ?? "";
            if (ViewModel.SubmissionError.Length > 0) return false;
            ViewModel.SubmissionError = CommitSubmission?.Invoke() ?? "";
            return ViewModel.SubmissionError.Length == 0;
        }
        catch (Exception ex) { ViewModel.SubmissionError = ex.Message; return false; }
        finally { IsEnabled = true; _submitting = false; }
    }

    protected override async void OnButtonClick(ContentDialogButton button)
    {
        if (_submitting) return;
        if (button == ContentDialogButton.Primary && !await TrySubmitAsync()) return;
        base.OnButtonClick(button);
    }
}

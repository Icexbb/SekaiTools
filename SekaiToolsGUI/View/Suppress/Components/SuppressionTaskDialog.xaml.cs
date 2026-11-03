using System.Windows;
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
    public bool OverwriteAuthorized => CheckBoxOverwrite.IsChecked == true;
    public Func<string>? CommitSubmission { get; set; }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary && !ViewModel.CanSubmit) return;
        if (button == ContentDialogButton.Primary)
        {
            ViewModel.SubmissionError = ValidateSubmission?.Invoke() ?? "";
            if (ViewModel.SubmissionError.Length > 0) return;
            if (ViewModel.OutputExists && !OverwriteAuthorized)
            {
                ViewModel.SubmissionError = "输出文件已存在，请修改路径或允许覆盖。";
                return;
            }
            try { ViewModel.SubmissionError = CommitSubmission?.Invoke() ?? ""; }
            catch (Exception ex) { ViewModel.SubmissionError = ex.Message; }
            if (ViewModel.SubmissionError.Length > 0) return;
        }
        base.OnButtonClick(button);
    }
}

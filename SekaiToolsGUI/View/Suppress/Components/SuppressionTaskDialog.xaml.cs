using System.Windows;
using SekaiToolsGUI.ViewModel.Suppress;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionTaskDialog : ContentDialog
{
    public SuppressionTaskDialog(ContentDialogHost contentPresenter,
        bool showAutoStart) : base(contentPresenter)
    {
        ViewModel = new SuppressTaskSettingsModel();
        DataContext = ViewModel;
        InitializeComponent();
        TaskSettings.DataContext = ViewModel;
        CheckBoxAutoStart.Visibility = showAutoStart ? Visibility.Visible : Visibility.Collapsed;
    }

    public SuppressTaskSettingsModel ViewModel { get; }
    public Func<string>? ValidateSubmission { get; set; }
    public bool AutoStart => CheckBoxAutoStart.IsChecked == true;

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary && !ViewModel.CanSubmit) return;
        if (button == ContentDialogButton.Primary)
        {
            ViewModel.SubmissionError = ValidateSubmission?.Invoke() ?? "";
            if (ViewModel.SubmissionError.Length > 0) return;
            if (ViewModel.OutputExists && CheckBoxOverwrite.IsChecked != true)
            {
                ViewModel.SubmissionError = "输出文件已存在，请修改路径或允许覆盖。";
                return;
            }
        }
        base.OnButtonClick(button);
    }
}

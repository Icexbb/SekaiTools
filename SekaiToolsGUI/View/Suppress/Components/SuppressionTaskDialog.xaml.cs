using SekaiToolsGUI.ViewModel.Suppress;
using SekaiToolsMedia;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressionTaskDialog : ContentDialog
{
    private readonly Action<VideoSuppressionOptions> _submit;

    public SuppressionTaskDialog(ContentDialogHost contentPresenter, Action<VideoSuppressionOptions> submit) : base(contentPresenter)
    {
        _submit = submit;
        ViewModel = new SuppressTaskSettingsModel();
        DataContext = ViewModel;
        InitializeComponent();
        TaskSettings.DataContext = ViewModel;
    }

    public SuppressTaskSettingsModel ViewModel { get; }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary)
        {
            try
            {
                if (!ViewModel.CanSubmit) return;
                _submit(ViewModel.ToOptions());
            }
            catch (Exception ex)
            {
                ViewModel.SubmissionError = ex.Message;
                return;
            }
        }
        base.OnButtonClick(button);
    }
}

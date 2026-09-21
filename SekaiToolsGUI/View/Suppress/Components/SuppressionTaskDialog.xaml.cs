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
    public bool AutoStart => CheckBoxAutoStart.IsChecked == true;

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary && !ViewModel.CanSubmit) return;
        base.OnButtonClick(button);
    }
}

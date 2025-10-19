using SekaiToolsGUI.ViewModel.General;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.General;

public partial class RefreshWaitDialog : ContentDialog
{
    public RefreshWaitDialog(string message = "")
    {
        InitializeComponent();
        DataContext = new RefreshWaitDialogModel();
        ViewModel.Message = message;
        UpdateLayout();
    }

    private RefreshWaitDialogModel ViewModel => (RefreshWaitDialogModel)DataContext;
}
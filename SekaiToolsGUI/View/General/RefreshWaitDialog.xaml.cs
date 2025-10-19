using SekaiToolsGUI.ViewModel.General;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.General;

public partial class RefreshWaitDialog : ContentDialog
{
    private RefreshWaitDialogModel ViewModel => (RefreshWaitDialogModel)DataContext;
    public RefreshWaitDialog(string message = "") : base()
    {
        InitializeComponent();
        DataContext = new RefreshWaitDialogModel();
        ViewModel.Message = message;
        UpdateLayout();
    }
}
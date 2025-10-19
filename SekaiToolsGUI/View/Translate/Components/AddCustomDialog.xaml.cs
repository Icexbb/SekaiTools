using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class AddCustomDialog : ContentDialog
{
    public AddCustomDialog(ContentPresenter contentPresenter) : base(contentPresenter)
    {
        DataContext = new AddCustomDialogModel();
        InitializeComponent();
    }

    public AddCustomDialogModel ViewModel => (AddCustomDialogModel)DataContext;
}
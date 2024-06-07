using System.Windows.Controls;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate;

public class AddCustomDialogModel : ViewModelBase
{
    public string CustomCharacter
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}

public partial class AddCustomDialog : ContentDialog
{
    public AddCustomDialogModel ViewModel => (AddCustomDialogModel)DataContext;

    public AddCustomDialog(ContentPresenter contentPresenter) : base(contentPresenter)
    {
        DataContext = new AddCustomDialogModel();
        InitializeComponent();
    }
}
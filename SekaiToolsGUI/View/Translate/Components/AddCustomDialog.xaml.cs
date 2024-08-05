using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

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
    public AddCustomDialog(ContentPresenter contentPresenter) : base(contentPresenter)
    {
        DataContext = new AddCustomDialogModel();
        InitializeComponent();
    }

    public AddCustomDialogModel ViewModel => (AddCustomDialogModel)DataContext;
}
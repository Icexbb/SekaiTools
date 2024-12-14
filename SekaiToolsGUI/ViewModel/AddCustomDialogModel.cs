namespace SekaiToolsGUI.ViewModel;

public class AddCustomDialogModel : ViewModelBase
{
    public string CustomCharacter
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}
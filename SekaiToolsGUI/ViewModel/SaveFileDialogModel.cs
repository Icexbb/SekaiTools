namespace SekaiToolsGUI.ViewModel;

public class SaveFileDialogModel : ViewModelBase
{
    public string FileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}
namespace SekaiToolsGUI.ViewModel.General;

public class RefreshWaitDialogModel : ViewModelBase
{
    public bool HasProgress
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public double Percentage
    {
        get => GetProperty(0.0);
        set => SetProperty(value);
    }

    public string ProgressText
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string FileDetails
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string Message
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}

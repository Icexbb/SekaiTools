namespace SekaiToolsGUI.ViewModel;

public class TaskControlModel : ViewModelBase
{
    public TaskSettingModel SettingModel { get; } = new();

    public bool Running
    {
        get => SettingModel.Running;
        set
        {
            SettingModel.Running = value;
            OnPropertyChanged();
        }
    }

    public double Progress
    {
        get => GetProperty<double>(0);
        set => SetProperty(value);
    }

    public string Status
    {
        get => GetProperty<string>("准备就绪");
        set => SetProperty(value);
    }


    public string ExtraMsg
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }
}
namespace SekaiToolsGUI.ViewModel;

public class TaskControlModel : ViewModelBase
{
    public TaskSettingModel SettingModel { get; set; } = new TaskSettingModel();
    private bool _running;

    public bool Running
    {
        get => _running;
        set
        {
            _running = value;
            SettingModel.Running = value;
            OnPropertyChanged();
        }
    }

    private double _progress;

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged();
        }
    }

    private string _status = "准备就绪";

    public string Status
    {
        set
        {
            _status = value;
            OnPropertyChanged();
        }
        get => _status;
    }

    private string _extraMsg = "";

    public string ExtraMsg
    {
        set
        {
            _extraMsg = value;
            OnPropertyChanged();
        }
        get => _extraMsg;
    }
}
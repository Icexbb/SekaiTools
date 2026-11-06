namespace SekaiToolsGUI.ViewModel.Setting;

public partial class SettingPageModel
{
    public bool DeveloperMode
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string DeveloperApiStatus
    {
        get => GetProperty("已关闭");
        internal set => base.SetProperty(value);
    }
}

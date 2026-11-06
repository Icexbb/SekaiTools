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
        internal set
        {
            base.SetProperty(value);
            OnPropertyChanged(nameof(DeveloperApiAddress));
            OnPropertyChanged(nameof(DeveloperApiToken));
            OnPropertyChanged(nameof(IsDeveloperApiConnected));
            OnPropertyChanged(nameof(DeveloperApiConnectionStatus));
        }
    }
    public bool IsDeveloperApiConnected => DeveloperApiStatus.StartsWith("http://127.0.0.1:", StringComparison.Ordinal);
    public string DeveloperApiAddress => IsDeveloperApiConnected ? DeveloperApiStatus.Split('\n')[0] : "";
    public string DeveloperApiToken => IsDeveloperApiConnected ? DeveloperApiStatus.Split("Bearer ").Last() : "";
    public string DeveloperApiConnectionStatus => IsDeveloperApiConnected ? "正在监听本机请求 · 令牌在本次启用期间有效" : DeveloperApiStatus;
}

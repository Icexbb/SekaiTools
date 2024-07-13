using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using SekaiDataFetch;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public class SettingPageModel : ViewModelBase
{
    public int CurrentApplicationTheme
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            SaveSetting();
            if (value == 3)
            {
                // ApplicationThemeManager.Apply(ApplicationTheme.Unknown);
                // SystemThemeWatcher.Watch(Application.Current.MainWindow);
            }
            else
            {
                // SystemThemeWatcher.UnWatch(Application.Current.MainWindow);
                switch (value)
                {
                    case 0:
                        ApplicationThemeManager.Apply(ApplicationTheme.Light);
                        break;
                    case 1:
                        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                        break;
                    case 2:
                        ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                        break;
                }
            }
        }
    }

    public readonly List<string> CustomSpecialCharacters = [];

    public int ProxyType
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            ProxyChangeable = value == 0 ? Visibility.Collapsed : Visibility.Visible;
            SaveSetting();
        }
    }

    public Visibility ProxyChangeable
    {
        get => GetProperty(Visibility.Collapsed);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }


    public string ProxyHost
    {
        get => GetProperty("127.0.0.1");
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public int ProxyPort
    {
        get => GetProperty(1080);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public int TypewriterFadeTime
    {
        get => GetProperty(50);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public int TypewriterCharTime
    {
        get => GetProperty(80);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public double ThresholdNormal
    {
        get => GetProperty(0.7d);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public double ThresholdSpecial
    {
        get => GetProperty(0.55d);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public Proxy GetProxy() => new(ProxyHost, ProxyPort, ProxyType switch
    {
        0 => Proxy.Type.None,
        1 => Proxy.Type.Http,
        2 => Proxy.Type.Socks5,
        _ => throw new ArgumentOutOfRangeException()
    });

    // public string ProxyUsername
    // {
    //     get => GetProperty("");
    //     set => SetProperty(value);
    // }
    //
    // public string ProxyPassword
    // {
    //     get => GetProperty("");
    //     set => SetProperty(value);
    // }

    public string FontFamily
    {
        get => GetProperty("思源黑体 CN Bold");
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public bool ExportComment
    {
        get => GetProperty(true);
        set
        {
            SetProperty(value);
            SaveSetting();
        }
    }

    public struct Setting
    {
        public int CurrentApplicationTheme { get; init; }
        public string[] CustomSpecialCharacters { get; init; }

        public int ProxyType { get; init; }
        public string ProxyHost { get; init; }
        public int ProxyPort { get; init; }
        public string ProxyUsername { get; init; }
        public string ProxyPassword { get; init; }

        public int TypewriterFadeTime { get; init; }
        public int TypewriterCharTime { get; init; }
        public double ThresholdNormal { get; init; }
        public double ThresholdSpecial { get; init; }

        public bool ExportComment { get; init; }

        public string FontFamily { get; init; }
    }


    private static string GetSettingPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "setting.json");

    public void SaveSetting()
    {
        var setting = new Setting
        {
            CurrentApplicationTheme = CurrentApplicationTheme,
            CustomSpecialCharacters = CustomSpecialCharacters.ToArray(),
            ProxyType = ProxyType,
            ProxyHost = ProxyHost,
            ProxyPort = ProxyPort,
            // ProxyUsername = ProxyUsername,
            // ProxyPassword = ProxyPassword

            TypewriterFadeTime = TypewriterFadeTime,
            TypewriterCharTime = TypewriterCharTime,

            ThresholdNormal = ThresholdNormal,
            ThresholdSpecial = ThresholdSpecial,

            FontFamily = FontFamily,

            ExportComment = ExportComment
        };
        var json = JsonConvert.SerializeObject(setting);
        Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
        File.WriteAllText(GetSettingPath(), json, Encoding.UTF8);
    }

    public void LoadSetting()
    {
        if (!File.Exists(GetSettingPath())) return;
        var json = File.ReadAllText(GetSettingPath(), Encoding.UTF8);
        var setting = JsonConvert.DeserializeObject<Setting>(json);
        CurrentApplicationTheme = setting.CurrentApplicationTheme;
        CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
        ProxyType = setting.ProxyType;
        ProxyHost = setting.ProxyHost;
        ProxyPort = setting.ProxyPort;
        // ProxyUsername = setting.ProxyUsername;
        // ProxyPassword = setting.ProxyPassword;
        TypewriterFadeTime = setting.TypewriterFadeTime == 0 ? 50 : setting.TypewriterFadeTime;
        TypewriterCharTime = setting.TypewriterCharTime == 0 ? 80 : setting.TypewriterCharTime;

        ThresholdNormal = setting.ThresholdNormal == 0 ? 0.7 : setting.ThresholdNormal;
        ThresholdSpecial = setting.ThresholdSpecial == 0 ? 0.55 : setting.ThresholdSpecial;

        FontFamily = setting.FontFamily == "" ? "思源黑体 CN Bold" : setting.FontFamily;

        ExportComment = setting.ExportComment;

        SaveSetting();
    }

    public SettingPageModel() => LoadSetting();

    public static string AppVersion
        => (Application.ResourceAssembly.GetName().Version ??
            new Version(0, 0, 0, 0)).ToString();
}

public partial class SettingPage : UserControl, INavigableView<SettingPageModel>
{
    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private int _devClickCount;

    private void DevClick(object sender, RoutedEventArgs e)
    {
        _devClickCount++;
        if (_devClickCount == 5)
        {
            ControlThreshold.Visibility = Visibility.Visible;
        }
    }

    public SettingPage()
    {
        DataContext = ((MainWindowViewModel)Application.Current.MainWindow!.DataContext).SettingPageModel;
        InitializeComponent();
    }

    private async void ChooseFont(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new FontSelectDialog(ViewModel.FontFamily);

        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        ViewModel.FontFamily = dialog.FontName;
    }
}
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using SekaiDataFetch;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public struct Setting
{
    public string AppVersion { get; init; }
    public int CurrentApplicationTheme { get; init; }
    public string[] CustomSpecialCharacters { get; init; }

    public int ProxyType { get; init; }
    public string ProxyHost { get; init; }

    public int ProxyPort { get; init; }

    // public string ProxyUsername { get; init; }
    // public string ProxyPassword { get; init; }

    public int TypewriterFadeTime { get; init; }
    public int TypewriterCharTime { get; init; }
    public double ThresholdNormal { get; init; }
    public double ThresholdSpecial { get; init; }

    public bool ExportComment { get; init; }

    public string FontFamily { get; init; }

    public static Setting Default => new()
    {
        ProxyType = 0,
        ProxyHost = "127.0.0.1",
        ProxyPort = 1080,
        TypewriterFadeTime = 50,
        TypewriterCharTime = 80,
        ThresholdNormal = 0.7,
        ThresholdSpecial = 0.7,
        ExportComment = true,
        FontFamily = "思源黑体 CN Bold",
    };

    public static Setting FromModel(SettingPageModel model) => new()
    {
        AppVersion = SettingPageModel.AppVersion,
        CurrentApplicationTheme = model.CurrentApplicationTheme,
        CustomSpecialCharacters = model.CustomSpecialCharacters.ToArray(),
        ProxyType = model.ProxyType,
        ProxyHost = model.ProxyHost,
        ProxyPort = model.ProxyPort,
        TypewriterFadeTime = model.TypewriterFadeTime,
        TypewriterCharTime = model.TypewriterCharTime,
        ThresholdNormal = model.ThresholdNormal,
        ThresholdSpecial = model.ThresholdSpecial,
        FontFamily = model.FontFamily,
        ExportComment = model.ExportComment
    };

    public string Dump() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public static Setting Load(string filepath)
    {
        return !File.Exists(filepath) ? Default : JsonConvert.DeserializeObject<Setting>(File.ReadAllText(filepath));
    }
}

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

    private static string GetSettingPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "setting.json");

    public void SaveSetting()
    {
        var setting = Setting.FromModel(this);
        Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
        File.WriteAllText(GetSettingPath(), setting.Dump(), Encoding.UTF8);
        Console.WriteLine("Setting saved");
    }

    public void LoadSetting()
    {
        var setting = Setting.Load(GetSettingPath());

        CurrentApplicationTheme = setting.CurrentApplicationTheme;
        CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
        ProxyType = setting.ProxyType;
        ProxyHost = setting.ProxyHost;
        ProxyPort = setting.ProxyPort;

        if (AppVersion != setting.AppVersion)
        {
            TypewriterFadeTime = Setting.Default.TypewriterFadeTime;
            TypewriterCharTime = Setting.Default.TypewriterCharTime;
            ThresholdNormal = Setting.Default.ThresholdNormal;
            ThresholdSpecial = Setting.Default.ThresholdSpecial;
            FontFamily = Setting.Default.FontFamily;
            ExportComment = Setting.Default.ExportComment;
        }
        else
        {
            TypewriterFadeTime = setting.TypewriterFadeTime;
            TypewriterCharTime = setting.TypewriterCharTime;
            ThresholdNormal = setting.ThresholdNormal;
            ThresholdSpecial = setting.ThresholdSpecial;
            FontFamily = setting.FontFamily == "" ? Setting.Default.FontFamily : setting.FontFamily;
            ExportComment = setting.ExportComment;
        }

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
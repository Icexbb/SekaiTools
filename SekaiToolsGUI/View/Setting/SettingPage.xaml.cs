using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch;
using SekaiToolsCore.Process;
using SekaiToolsGUI.View.Setting.Components;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public struct Setting
{
    public Setting()
    {
    }

    public string AppVersion { get; init; } = "1.0.0";
    public int CurrentApplicationTheme { get; init; } = 0;
    public string[] CustomSpecialCharacters { get; init; } = [];

    public int ProxyType { get; init; } = 0;
    public string ProxyHost { get; init; } = "127.0.0.1";
    public int ProxyPort { get; init; } = 1080;

    // public string ProxyUsername { get; init; }
    // public string ProxyPassword { get; init; }

    public int TypewriterFadeTime { get; init; } = 50;
    public int TypewriterCharTime { get; init; } = 80;
    public double ThresholdNormal { get; init; } = 0.7;
    public double ThresholdSpecial { get; init; } = 0.7;
    public string DialogFontFamily { get; init; } = "思源黑体 CN Bold";
    public string BannerFontFamily { get; init; } = "思源黑体 Medium";
    public string MarkerFontFamily { get; init; } = "思源黑体 Medium";

    public bool ExportLine1 { get; init; } = true;
    public bool ExportLine2 { get; init; } = true;
    public bool ExportLine3 { get; init; } = true;
    public bool ExportCharacter { get; init; } = true;
    public bool ExportBannerMask { get; init; } = true;
    public bool ExportBannerText { get; init; } = true;
    public bool ExportMarkerMask { get; init; } = true;
    public bool ExportMarkerText { get; init; } = true;
    public bool ExportScreenComment { get; init; } = true;

    public static Setting Default => new()
    {
        ProxyType = 0,
        ProxyHost = "127.0.0.1",
        ProxyPort = 1080,
        TypewriterFadeTime = 50,
        TypewriterCharTime = 80,
        ThresholdNormal = 0.7,
        ThresholdSpecial = 0.7,
        DialogFontFamily = "思源黑体 CN Bold",
        BannerFontFamily = "思源黑体 Medium",
        MarkerFontFamily = "思源黑体 Medium",
        ExportLine1 = true,
        ExportLine2 = true,
        ExportLine3 = true,
        ExportCharacter = true,
        ExportBannerMask = true,
        ExportBannerText = true,
        ExportMarkerMask = true,
        ExportMarkerText = true,
        ExportScreenComment = true
    };

    public static Setting FromModel(SettingPageModel model)
    {
        return new Setting
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

            DialogFontFamily = model.DialogFontFamily,
            BannerFontFamily = model.BannerFontFamily,
            MarkerFontFamily = model.MarkerFontFamily,

            ExportLine1 = model.ExportLine1,
            ExportLine2 = model.ExportLine2,
            ExportLine3 = model.ExportLine3,
            ExportCharacter = model.ExportCharacter,
            ExportBannerMask = model.ExportBannerMask,
            ExportBannerText = model.ExportBannerText,
            ExportMarkerMask = model.ExportMarkerMask,
            ExportMarkerText = model.ExportMarkerText,
            ExportScreenComment = model.ExportScreenComment,
        };
    }

    public string Dump()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        });
    }

    public static Setting Load(string filepath)
    {
        return !File.Exists(filepath)
            ? Default
            : JsonSerializer.Deserialize<Setting>(File.ReadAllText(filepath));
    }
}

public class SettingPageModel : ViewModelBase
{
    public readonly List<string> CustomSpecialCharacters = [];

    private SettingPageModel()
    {
        LoadSetting();
    }

    public static SettingPageModel Instance { get; } = new();

    public int CurrentApplicationTheme
    {
        get => GetProperty(0);
        set
        {
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

            SetProperty(value);
        }
    }

    public int ProxyType
    {
        get => GetProperty(0);
        set
        {
            ProxyChangeable = value == 0 ? Visibility.Collapsed : Visibility.Visible;
            SetProperty(value);
        }
    }

    public Visibility ProxyChangeable
    {
        get => GetProperty(Visibility.Collapsed);
        set => SetProperty(value);
    }

    public string ProxyHost
    {
        get => GetProperty("127.0.0.1");
        set => SetProperty(value);
    }

    public int ProxyPort
    {
        get => GetProperty(1080);
        set => SetProperty(value);
    }

    public int TypewriterFadeTime
    {
        get => GetProperty(50);
        set => SetProperty(value);
    }

    public int TypewriterCharTime
    {
        get => GetProperty(80);
        set => SetProperty(value);
    }

    public double ThresholdNormal
    {
        get => GetProperty(0.7d);
        set => SetProperty(value);
    }

    public double ThresholdSpecial
    {
        get => GetProperty(0.55d);
        set => SetProperty(value);
    }

    public string DialogFontFamily
    {
        get => GetProperty("思源黑体 CN Bold");
        set => SetProperty(value);
    }

    public string BannerFontFamily
    {
        get => GetProperty("思源黑体 Medium");
        set => SetProperty(value);
    }

    public string MarkerFontFamily
    {
        get => GetProperty("思源黑体 Medium");
        set => SetProperty(value);
    }

    public bool ExportLine1
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine2
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine3
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportCharacter
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportScreenComment
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public static string AppVersion
        => (Application.ResourceAssembly.GetName().Version ??
            new Version(0, 0, 0, 0)).ToString();

    private new void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        base.SetProperty(value, propertyName);
        SaveSetting();
    }

    public Proxy GetProxy()
    {
        return new Proxy(ProxyHost, ProxyPort, ProxyType switch
        {
            0 => Proxy.Type.None,
            1 => Proxy.Type.Http,
            2 => Proxy.Type.Socks5,
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    public ExportStyleConfig GetExportStyleConfig()
    {
        return new ExportStyleConfig
        {
            ExportLine1 = ExportLine1,
            ExportLine2 = ExportLine2,
            ExportLine3 = ExportLine3,
            ExportCharacter = ExportCharacter,
            ExportBannerMask = ExportBannerMask,
            ExportBannerText = ExportBannerText,
            ExportMarkerMask = ExportMarkerMask,
            ExportMarkerText = ExportMarkerText,
            ExportScreenComment = ExportScreenComment
        };
    }

    public StyleFontConfig GetStyleFontConfig()
    {
        return new StyleFontConfig
        {
            DialogFontFamily = DialogFontFamily,
            BannerFontFamily = BannerFontFamily,
            MarkerFontFamily = MarkerFontFamily
        };
    }

    public TypewriterSetting GetTypewriterSetting()
    {
        return new TypewriterSetting(TypewriterFadeTime, TypewriterCharTime);
    }

    public MatchingThreshold GetMatchingThreshold()
    {
        return new MatchingThreshold(ThresholdNormal, ThresholdSpecial);
    }

    private static string GetSettingPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "setting.json");
    }

    public void SaveSetting()
    {
        var setting = Setting.FromModel(this);
        Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
        File.WriteAllText(GetSettingPath(), setting.Dump(), Encoding.UTF8);
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
            DialogFontFamily = Setting.Default.DialogFontFamily;
            BannerFontFamily = Setting.Default.BannerFontFamily;
            MarkerFontFamily = Setting.Default.MarkerFontFamily;
            ExportLine1 = Setting.Default.ExportLine1;
            ExportLine2 = Setting.Default.ExportLine2;
            ExportLine3 = Setting.Default.ExportLine3;
            ExportCharacter = Setting.Default.ExportCharacter;
            ExportBannerMask = Setting.Default.ExportBannerMask;
            ExportBannerText = Setting.Default.ExportBannerText;
            ExportMarkerMask = Setting.Default.ExportMarkerMask;
            ExportMarkerText = Setting.Default.ExportMarkerText;
            ExportScreenComment = Setting.Default.ExportScreenComment;
        }
        else
        {
            TypewriterFadeTime = setting.TypewriterFadeTime;
            TypewriterCharTime = setting.TypewriterCharTime;
            ThresholdNormal = setting.ThresholdNormal;
            ThresholdSpecial = setting.ThresholdSpecial;
            DialogFontFamily = setting.DialogFontFamily == ""
                ? Setting.Default.DialogFontFamily
                : setting.DialogFontFamily;
            BannerFontFamily = setting.BannerFontFamily == ""
                ? Setting.Default.BannerFontFamily
                : setting.BannerFontFamily;
            MarkerFontFamily = setting.MarkerFontFamily == ""
                ? Setting.Default.MarkerFontFamily
                : setting.MarkerFontFamily;
            ExportLine1 = setting.ExportLine1;
            ExportLine2 = setting.ExportLine2;
            ExportLine3 = setting.ExportLine3;
            ExportCharacter = setting.ExportCharacter;
            ExportBannerMask = setting.ExportBannerMask;
            ExportBannerText = setting.ExportBannerText;
            ExportMarkerMask = setting.ExportMarkerMask;
            ExportMarkerText = setting.ExportMarkerText;
            ExportScreenComment = setting.ExportScreenComment;
        }

        SaveSetting();
    }
}

public partial class SettingPage : UserControl, INavigableView<SettingPageModel>
{
    private int _devClickCount;

    public SettingPage()
    {
        DataContext = MainWindowViewModel.SettingPageModel;
        InitializeComponent();
    }

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private void DevClick(object sender, RoutedEventArgs e)
    {
        _devClickCount++;
        if (_devClickCount == 5) ControlThreshold.Visibility = Visibility.Visible;
    }

    private async void ChooseDialogFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.DialogFontFamily = font;
    }

    private async void ChooseBannerFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.BannerFontFamily = font;
    }

    private async void ChooseMarkerFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.MarkerFontFamily = font;
    }

    private async Task<string> OpenFontDialog()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new FontSelectDialog(ViewModel.DialogFontFamily);
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        return dialogResult != ContentDialogResult.Primary ? "" : dialog.FontName;
    }
}
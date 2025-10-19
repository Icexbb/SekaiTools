using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using SekaiDataFetch;
using SekaiToolsCore;
using SekaiToolsCore.Process.Model;
using Wpf.Ui.Appearance;

namespace SekaiToolsGUI.ViewModel.Setting;

public partial class SettingPageModel : ViewModelBase
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
        return new TypewriterSetting
        {
            FadeTime = TypewriterFadeTime,
            CharTime = TypewriterCharTime
        };
    }
}

partial class SettingPageModel
{
    private static string GetSettingPath()
    {
        return Path.Combine(ResourceManager.DataBaseDir, "Data", "setting.json");
    }

    public void SaveSetting()
    {
        var setting = Model.Setting.FromModel(this);
        Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
        File.WriteAllText(GetSettingPath(), setting.Dump(), Encoding.UTF8);
    }

    public void LoadSetting()
    {
        var setting = Model.Setting.Load(GetSettingPath());

        CurrentApplicationTheme = setting.CurrentApplicationTheme;
        CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
        ProxyType = setting.ProxyType;
        ProxyHost = setting.ProxyHost;
        ProxyPort = setting.ProxyPort;

        if (AppVersion != setting.AppVersion)
        {
            TypewriterFadeTime = Model.Setting.Default.TypewriterFadeTime;
            TypewriterCharTime = Model.Setting.Default.TypewriterCharTime;

            DialogFontFamily = Model.Setting.Default.DialogFontFamily;
            BannerFontFamily = Model.Setting.Default.BannerFontFamily;
            MarkerFontFamily = Model.Setting.Default.MarkerFontFamily;

            ExportLine1 = Model.Setting.Default.ExportLine1;
            ExportLine2 = Model.Setting.Default.ExportLine2;
            ExportLine3 = Model.Setting.Default.ExportLine3;
            ExportCharacter = Model.Setting.Default.ExportCharacter;
            ExportBannerMask = Model.Setting.Default.ExportBannerMask;
            ExportBannerText = Model.Setting.Default.ExportBannerText;
            ExportMarkerMask = Model.Setting.Default.ExportMarkerMask;
            ExportMarkerText = Model.Setting.Default.ExportMarkerText;
            ExportScreenComment = Model.Setting.Default.ExportScreenComment;
        }
        else
        {
            TypewriterFadeTime = setting.TypewriterFadeTime;
            TypewriterCharTime = setting.TypewriterCharTime;

            DialogFontFamily = setting.DialogFontFamily == ""
                ? Model.Setting.Default.DialogFontFamily
                : setting.DialogFontFamily;
            BannerFontFamily = setting.BannerFontFamily == ""
                ? Model.Setting.Default.BannerFontFamily
                : setting.BannerFontFamily;
            MarkerFontFamily = setting.MarkerFontFamily == ""
                ? Model.Setting.Default.MarkerFontFamily
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
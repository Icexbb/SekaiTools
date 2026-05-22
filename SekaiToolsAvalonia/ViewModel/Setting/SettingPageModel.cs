using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using SekaiToolsBase;
using SekaiToolsCore;
using SekaiToolsCore.Process.Config;

namespace SekaiToolsAvalonia.ViewModel.Setting;

public partial class SettingPageModel : ViewModelBase
{
    public readonly List<string> CustomSpecialCharacters = [];

    private SettingPageModel()
    {
    }

    public static SettingPageModel Instance { get; } = new();

    public int CurrentApplicationTheme
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            ApplyTheme(value);
        }
    }

    private static void ApplyTheme(int theme)
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                0 => Avalonia.Styling.ThemeVariant.Light,
                1 => Avalonia.Styling.ThemeVariant.Dark,
                2 => Avalonia.Styling.ThemeVariant.Default,
                _ => Avalonia.Styling.ThemeVariant.Default
            };
        }
    }

    public int ProxyType
    {
        get => GetProperty(0);
        set
        {
            ProxyChangeable = value != 0;
            SetProperty(value);
        }
    }

    public bool ProxyChangeable
    {
        get => GetProperty(false);
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

    public bool ExportLine1 { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportLine2 { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportLine3 { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportCharacter { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportBannerMask { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportBannerText { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportMarkerMask { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportMarkerText { get => GetProperty(true); set => SetProperty(value); }
    public bool ExportScreenComment { get => GetProperty(true); set => SetProperty(value); }

    public static string StaticAppVersion => "1.5.2";
    public string AppVersion => StaticAppVersion;

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
            ExportLine1 = ExportLine1, ExportLine2 = ExportLine2, ExportLine3 = ExportLine3,
            ExportCharacter = ExportCharacter,
            ExportBannerMask = ExportBannerMask, ExportBannerText = ExportBannerText,
            ExportMarkerMask = ExportMarkerMask, ExportMarkerText = ExportMarkerText,
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
        return new TypewriterSetting { FadeTime = TypewriterFadeTime, CharTime = TypewriterCharTime };
    }

    private new void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        base.SetProperty(value, propertyName);
        SaveSetting();
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
        try
        {
            var setting = Model.Setting.FromModel(this);
            Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
            File.WriteAllText(GetSettingPath(), setting.Dump(), Encoding.UTF8);
        }
        catch (Exception) { }
    }

    public void ResetSetting()
    {
        ProxyType = Model.Setting.Default.ProxyType;
        ProxyHost = Model.Setting.Default.ProxyHost;
        ProxyPort = Model.Setting.Default.ProxyPort;
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
        SaveSetting();
    }

    public static void LoadSetting()
    {
        Model.Setting setting;
        try { setting = Model.Setting.Load(GetSettingPath()); }
        catch (Exception) { setting = new Model.Setting(); }

        var self = Instance;
        self.CurrentApplicationTheme = setting.CurrentApplicationTheme;
        self.CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
        self.ProxyType = setting.ProxyType;
        self.ProxyHost = setting.ProxyHost;
        self.ProxyPort = setting.ProxyPort;
        self.TypewriterFadeTime = setting.TypewriterFadeTime;
        self.TypewriterCharTime = setting.TypewriterCharTime;
        self.DialogFontFamily = setting.DialogFontFamily == "" ? Model.Setting.Default.DialogFontFamily : setting.DialogFontFamily;
        self.BannerFontFamily = setting.BannerFontFamily == "" ? Model.Setting.Default.BannerFontFamily : setting.BannerFontFamily;
        self.MarkerFontFamily = setting.MarkerFontFamily == "" ? Model.Setting.Default.MarkerFontFamily : setting.MarkerFontFamily;
        self.ExportLine1 = setting.ExportLine1;
        self.ExportLine2 = setting.ExportLine2;
        self.ExportLine3 = setting.ExportLine3;
        self.ExportCharacter = setting.ExportCharacter;
        self.ExportBannerMask = setting.ExportBannerMask;
        self.ExportBannerText = setting.ExportBannerText;
        self.ExportMarkerMask = setting.ExportMarkerMask;
        self.ExportMarkerText = setting.ExportMarkerText;
        self.ExportScreenComment = setting.ExportScreenComment;
    }
}

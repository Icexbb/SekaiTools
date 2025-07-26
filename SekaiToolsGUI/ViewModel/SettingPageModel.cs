using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using SekaiDataFetch;
using SekaiToolsCore.Process.Model;
using SekaiToolsGUI.Model;
using Wpf.Ui.Appearance;

namespace SekaiToolsGUI.ViewModel;

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


    private DownloadSourceEditorModel[]? _source;

    public DownloadSourceEditorModel[] Source
    {
        get
        {
            if (_source != null) return _source;
            LoadSource();
            return Source;
        }
        private set
        {
            _source = value;
            OnPropertyChanged();
        }
    }
}

partial class SettingPageModel
{
    private static string GetSettingPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "setting.json");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "source.json");
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

    public void LoadSource()
    {
        var source = SekaiDataFetch.Source.SourceData.Load(GetSourcePath());
        Source = new DownloadSourceEditorModel[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            Source[i] = new DownloadSourceEditorModel(this, source[i]);
        }
    }

    public void SaveSource()
    {
        var source = new SekaiDataFetch.Source.SourceData[Source.Length];
        for (var i = 0; i < Source.Length; i++)
        {
            source[i] = new SekaiDataFetch.Source.SourceData
            {
                SourceName = Source[i].SourceName,
                StorageBaseUrl = Source[i].StorageBaseUrl,
                SourceTemplate = Source[i].SourceTemplate,
                ActionSetTemplate = Source[i].ActionSetTemplate,
                MemberStoryTemplate = Source[i].MemberStoryTemplate,
                EventStoryTemplate = Source[i].EventStoryTemplate,
                SpecialStoryTemplate = Source[i].SpecialStoryTemplate,
                UnitStoryTemplate = Source[i].UnitStoryTemplate,
                Deletable = Source[i].Deletable
            };
        }

        File.WriteAllText(GetSourcePath(), SekaiDataFetch.Source.SourceData.Dump(source), Encoding.UTF8);
    }

    public void NewSource()
    {
        var source = new SekaiDataFetch.Source.SourceData
        {
            SourceName = "New Source",
            SourceTemplate = "https://example.com/{type}.json",
            StorageBaseUrl = "https://example.com/",
            ActionSetTemplate = "actionset/{abName}/{scenarioId}.json",
            MemberStoryTemplate = "member/{abName}/{scenarioId}.json",
            EventStoryTemplate = "event/{abName}/{scenarioId}.json",
            SpecialStoryTemplate = "special/{abName}/{scenarioId}.json",
            UnitStoryTemplate = "unit/{abName}/{scenarioId}.json",
            Deletable = true
        };
        Array.Resize(ref _source, Source.Length + 1);
        Source[^1] = new DownloadSourceEditorModel(this, source);
        OnPropertyChanged(nameof(Source));
        SaveSource();
    }

    public void ResetSource()
    {
        var sourceData = SekaiDataFetch.Source.SourceData.Default;
        var source = new DownloadSourceEditorModel[sourceData.Length];
        for (var i = 0; i < sourceData.Length; i++)
        {
            source[i] = new DownloadSourceEditorModel(this, sourceData[i]);
        }

        Source = source;
        OnPropertyChanged(nameof(Source));
        SaveSource();
    }

    public void DeleteSource(DownloadSourceEditorModel source)
    {
        var index = Array.IndexOf(Source, source);
        if (index == -1) return;
        DeleteSource(index);
    }

    private void DeleteSource(int index)
    {
        var newSource = new DownloadSourceEditorModel[Source.Length - 1];
        for (var i = 0; i < Source.Length; i++)
        {
            if (i == index) continue;
            newSource[i > index ? i - 1 : i] = Source[i];
        }

        Source = newSource;
        OnPropertyChanged(nameof(Source));
        SaveSource();
    }
}
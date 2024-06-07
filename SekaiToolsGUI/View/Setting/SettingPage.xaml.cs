using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using SekaiToolsGUI.ViewModel;
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

    public static string AppVersion
        => (Application.ResourceAssembly.GetName().Version ??
            new Version(0, 0, 0, 0)).ToString();

    public struct Setting
    {
        public int CurrentApplicationTheme { get; init; }
        public string[] CustomSpecialCharacters { get; init; }
    }


    private static string GetSettingPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SekaiTools",
            "setting.json");

    public void SaveSetting()
    {
        var setting = new Setting
        {
            CurrentApplicationTheme = CurrentApplicationTheme,
            CustomSpecialCharacters = CustomSpecialCharacters.ToArray()
        };
        var json = JsonConvert.SerializeObject(setting);
        Directory.CreateDirectory(Path.GetDirectoryName(GetSettingPath())!);
        File.WriteAllText(GetSettingPath(), json, Encoding.UTF8);
    }

    private void LoadSetting()
    {
        if (!File.Exists(GetSettingPath())) return;
        var json = File.ReadAllText(GetSettingPath(), Encoding.UTF8);
        var setting = JsonConvert.DeserializeObject<Setting>(json);
        CurrentApplicationTheme = setting.CurrentApplicationTheme;
        CustomSpecialCharacters.AddRange(setting.CustomSpecialCharacters);
        SaveSetting();
    }

    public SettingPageModel() => LoadSetting();
}

public partial class SettingPage : UserControl, INavigableView<SettingPageModel>
{
    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    public SettingPage()
    {
        DataContext = ((MainWindowViewModel)Application.Current.MainWindow!.DataContext).SettingPageModel;
        InitializeComponent();
    }
}
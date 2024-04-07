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

    public string AppVersion
        => (Application.ResourceAssembly.GetName().Version ??
            new Version(0, 0, 0, 0)).ToString();

    public struct Setting
    {
        public int CurrentApplicationTheme { get; set; }
    }

    private Setting ToSetting()
    {
        return new Setting
        {
            CurrentApplicationTheme = CurrentApplicationTheme
        };
    }

    private void SaveSetting()
    {
        var setting = ToSetting();
        var json = JsonConvert.SerializeObject(setting);
        File.WriteAllText("setting.json", json, Encoding.UTF8);
    }

    private void LoadSetting()
    {
        if (!File.Exists("setting.json")) return;
        var json = File.ReadAllText("setting.json", Encoding.UTF8);
        var setting = JsonConvert.DeserializeObject<Setting>(json);
        CurrentApplicationTheme = setting.CurrentApplicationTheme;
        SaveSetting();
    }

    public SettingPageModel()
    {
        LoadSetting();
    }
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
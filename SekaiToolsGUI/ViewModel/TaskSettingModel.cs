using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace SekaiToolsGUI.ViewModel;

public class TaskSettingModel : ViewModelBase
{
    public string VideoFilePath
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string ScriptFilePath
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }


    public string TranslateFilePath
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }


    public string OutputFilePath
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public string Id
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public int TypewriterFade
    {
        get => GetProperty<int>(defaultValue: 50);
        set => SetProperty(value);
    }

    public int TypewriterChar
    {
        get => GetProperty<int>(defaultValue: 80);
        set => SetProperty(value);
    }


    public bool Running
    {
        get => GetProperty<bool>(false);
        set => SetProperty(value);
    }
}
using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace SekaiToolsGUI.ViewModel;

public class TaskSettingModel : ViewModelBase
{
    private string _videoFilePath = "";

    public string VideoFilePath
    {
        get => _videoFilePath;
        set
        {
            _videoFilePath = value;
            OnPropertyChanged();
        }
    }

    private string _scriptFilePath = "";

    public string ScriptFilePath
    {
        get => _scriptFilePath;
        set
        {
            _scriptFilePath = value;
            OnPropertyChanged();
        }
    }

    private string _translateFilePath = "";

    public string TranslateFilePath
    {
        get => _translateFilePath;
        set
        {
            _translateFilePath = value;
            OnPropertyChanged();
        }
    }

    private string _outputFilePath = "";

    public string OutputFilePath
    {
        get => _outputFilePath;
        set
        {
            _outputFilePath = value;
            OnPropertyChanged();
        }
    }

    public string Id { get; set; } = "";

    private int _typewriterFade = 50, _typewriterChar = 80;

    public int TypewriterFade
    {
        get => _typewriterFade;
        set
        {
            _typewriterFade = value;
            OnPropertyChanged();
        }
    }

    public int TypewriterChar
    {
        get => _typewriterChar;
        set
        {
            _typewriterChar = value;
            OnPropertyChanged();
        }
    }

    private bool _running;

    public bool Running
    {
        get => _running;
        set
        {
            _running = value;
            OnPropertyChanged();
        }
    }
}

public class FilepathValidationRule : ValidationRule
{
    public bool Required { get; set; }


    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var text = (value as string)!;
        if (Required && text.Length == 0) return new ValidationResult(false, "Text cannot be empty.");

        if (!File.Exists(text)) return new ValidationResult(false, "File Not Found");

        return ValidationResult.ValidResult;
    }
}
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SekaiToolsGUI.ViewModel;

public class TranslationPartModel : ViewModelBase
{
    public string OriginalContent
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public bool IsDialog
    {
        get => GetProperty<bool>(true);
        set => SetProperty(value);
    }

    public string OriginalCharacter
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedCharacter
    {
        get => GetProperty<string>(string.Empty);
        set => SetProperty(value);
    }

    public int CharacterId
    {
        get => GetProperty<int>(0);
        set => SetProperty(value);
    }

    public string CharacterImage =>
        CharacterId is > 0 and <= 31
            ? $"pack://application:,,,/SekaiToolsGUI;component/Resources/Characters/chr_{CharacterId}.png"
            : string.Empty;
}
using SekaiDataFetch.List;

namespace SekaiToolsGUI.ViewModel;

public class CharacterComboBoxItem
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public string Source { get; set; } = "";
}

public class CardStoryTabModel : ViewModelBase
{
    public CharacterComboBoxItem[] Characters
    {
        get => GetProperty<CharacterComboBoxItem[]>([]);
        set => SetProperty(value);
    }

    public CardStoryImpl[] CardStories
    {
        get => GetProperty<CardStoryImpl[]>([]);
        set => SetProperty(value);
    }
}
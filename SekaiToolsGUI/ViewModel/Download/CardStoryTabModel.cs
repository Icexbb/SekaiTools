using SekaiDataFetch.Item;

namespace SekaiToolsGUI.ViewModel.Download;

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

    public CardStorySet[] CardStories
    {
        get => GetProperty<CardStorySet[]>([]);
        set => SetProperty(value);
    }
}
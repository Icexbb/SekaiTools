using SekaiDataFetch.Item;

namespace SekaiToolsGUI.ViewModel.Download;

public class EventStoryTabModel : ViewModelBase
{
    public CharacterComboBoxItem[] Characters
    {
        get => GetProperty<CharacterComboBoxItem[]>([]);
        set => SetProperty(value);
    }

    public bool UseStoryIndex
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public EventStorySet[] EventStories
    {
        get => GetProperty<EventStorySet[]>([]);
        set => SetProperty(value);
    }
}

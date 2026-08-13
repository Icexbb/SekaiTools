using SekaiDataFetch.Item;
using SekaiToolsBase.DataList;

namespace SekaiToolsGUI.ViewModel.Download;

public class ActionStoryTabModel : ViewModelBase
{
    public CharacterComboBoxItem[] Characters
    {
        get => GetProperty<CharacterComboBoxItem[]>([]);
        set => SetProperty(value);
    }

    public Area[] Areas
    {
        get => GetProperty<Area[]>([]);
        set => SetProperty(value);
    }

    public AreaStorySet[] EventStories
    {
        get => GetProperty<AreaStorySet[]>([]);
        set => SetProperty(value);
    }

}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryTab : UserControl, IRefreshable
{
    private int _currentDirection = -1;

    public EventStoryTab()
    {
        DataContext ??= new EventStoryTabModel();
        InitializeComponent();
        InitializeCharacterComboBox();
    }

    private EventStoryTabModel ViewModel => (EventStoryTabModel)DataContext;
    private ListEventStory ListEventStory => ListEventStory.Instance;

    public async Task Refresh()
    {
        CardCharacters.IsEnabled = false;
        CardUnits.IsEnabled = false;
        try
        {
            ListEventStory.SetSource(GetSourceType());
            ListEventStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await ListEventStory.Refresh();
            RefreshItems();
        }
        finally
        {
            CardCharacters.IsEnabled = true;
            CardUnits.IsEnabled = true;
        }
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }

    private void InitializeCharacterComboBox()
    {
        var characters = CharacterFilterOptions.CreateItems();

        ViewModel.Characters = characters;
        var groupedView = CollectionViewSource.GetDefaultView(characters);
        groupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CharacterComboBoxItem.GroupName)));
        CharacterComboBox.ItemsSource = groupedView;
        CharacterComboBox.SelectedIndex = 0;
    }

    private void CharacterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void Filter_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void EventStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        BoxType.SelectedIndex = 0;
        if (CharacterComboBox.SelectedIndex < 0) CharacterComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    private void ButtonSort_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonSort.RenderTransform = new ScaleTransform(-1, _currentDirection);
        _currentDirection *= -1;
        RefreshItems();
    }

    private void RefreshItems()
    {
        if (ListEventStory.Data.Count == 0) return;

        var data = ListEventStory.Data.Select(item => (EventStorySet)item.Clone()).ToList();
        data.Sort((x, y) => _currentDirection * x.EventStory.EventId.CompareTo(y.EventStory.EventId));
        ViewModel.EventStories = data.Where(JudgeVisibility).ToArray();
    }

    private bool JudgeVisibility(EventStorySet data)
    {
        string[] filterTypes = BoxType.SelectedIndex switch
        {
            0 => ["marathon", "cheerful_carnival", "world_bloom"],
            1 => ["marathon"],
            2 => ["cheerful_carnival"],
            3 => ["world_bloom"],
            _ => throw new ArgumentOutOfRangeException()
        };

        if (!filterTypes.Contains(data.GameEvent.EventType)) return false;

        if (CharacterComboBox.SelectedItem is not CharacterComboBoxItem character || character.Value == 0)
            return true;

        return data.GameEvent.EventType == "world_bloom"
            ? data.BannerGameCharacterIds.Contains(character.GameCharacterId)
            : data.EventStory.BannerGameCharacterUnitId == character.Value;
    }
}
